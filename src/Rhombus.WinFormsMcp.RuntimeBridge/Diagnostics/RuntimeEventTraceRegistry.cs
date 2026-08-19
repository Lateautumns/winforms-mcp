using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Diagnostics;

/// <summary>
/// Owns bounded, read-only WinForms event subscriptions. Sessions are always
/// detached on stop/expiry so a trace cannot retain controls indefinitely.
/// </summary>
internal sealed class RuntimeEventTraceRegistry : IDisposable {
    private const int MaxEvidenceTextLength = 4_096;
    private static readonly string[] DefaultEvents = [
        "Click", "TextChanged", "CheckedChanged", "SelectedIndexChanged",
        "VisibleChanged", "EnabledChanged", "FormClosing"
    ];
    private readonly object _gate = new();
    private readonly RuntimeBridgeOptions _options;
    private readonly Action<Action> _postToUi;
    private readonly Dictionary<string, TraceSession> _sessions = new(StringComparer.Ordinal);
    private bool _disposed;

    public RuntimeEventTraceRegistry(RuntimeBridgeOptions options, Action<Action>? postToUi = null) {
        _options = options;
        _postToUi = postToUi ?? (action => action());
    }

    public RuntimeEventTraceSnapshot Start(
        IReadOnlyList<TraceControlTarget> controls,
        IReadOnlyCollection<string>? requestedEvents,
        int maxEvents,
        int durationMs) {
        lock (_gate) {
            ThrowIfDisposed();
            ExpireSessionsUnsafe();
            var eventNames = NormalizeEvents(requestedEvents);
            var boundedEvents = Clamp(maxEvents, 1, Math.Max(1, _options.MaxEventTraceEvents));
            var boundedDuration = Clamp(durationMs, 1, Math.Max(1, _options.MaxEventTraceDurationMs));
            while (_sessions.Count >= Math.Max(1, _options.MaxEventTraceSessions))
                RemoveOldestUnsafe();

            var session = new TraceSession(
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMilliseconds(boundedDuration),
                boundedEvents,
                eventNames);
            foreach (var target in controls.Take(Math.Max(1, _options.MaxEventTraceControls)))
                session.Subscribe(target);
            _sessions[session.TraceId] = session;
            session.ScheduleExpiration(() => {
                try {
                    _postToUi(() => Expire(session.TraceId));
                }
                catch {
                    // Host shutdown performs a final synchronous cleanup.
                }
            });
            return session.Snapshot();
        }
    }

    public RuntimeEventTraceSnapshot Read(
        string traceId,
        long afterSequence,
        int maxEvents) {
        lock (_gate) {
            ThrowIfDisposed();
            ExpireSessionsUnsafe();
            if (!_sessions.TryGetValue(traceId, out var session))
                throw new InvalidOperationException($"Event trace '{traceId}' was not found or has expired.");
            return session.Snapshot(afterSequence, Clamp(maxEvents, 1, session.MaxEvents));
        }
    }

    public RuntimeEventTraceSnapshot Stop(string traceId) {
        lock (_gate) {
            ThrowIfDisposed();
            ExpireSessionsUnsafe();
            if (!_sessions.TryGetValue(traceId, out var session))
                throw new InvalidOperationException($"Event trace '{traceId}' was not found or has expired.");
            _sessions.Remove(traceId);
            session.Dispose();
            return session.Snapshot(active: false);
        }
    }

    public void Dispose() {
        lock (_gate) {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var session in _sessions.Values)
                session.Dispose();
            _sessions.Clear();
        }
    }

    private void ExpireSessionsUnsafe() {
        var expired = _sessions.Values
            .Where(session => session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            .ToArray();
        foreach (var session in expired) {
            _sessions.Remove(session.TraceId);
            session.Dispose();
        }
    }

    private void RemoveOldestUnsafe() {
        var oldest = _sessions.Values.OrderBy(session => session.StartedAtUtc).FirstOrDefault();
        if (oldest is null)
            return;
        _sessions.Remove(oldest.TraceId);
        oldest.Dispose();
    }

    private void Expire(string traceId) {
        lock (_gate) {
            if (_disposed || !_sessions.TryGetValue(traceId, out var session) ||
                session.ExpiresAtUtc > DateTimeOffset.UtcNow)
                return;
            _sessions.Remove(traceId);
            session.Dispose();
        }
    }

    private static string[] NormalizeEvents(IReadOnlyCollection<string>? requestedEvents) {
        var source = requestedEvents is { Count: > 0 } ? requestedEvents : DefaultEvents;
        var result = source
            .Where(eventName => eventName is not null)
            .Select(eventName => DefaultEvents.FirstOrDefault(
                supported => supported.Equals(eventName, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (result.Length == 0)
            throw new ArgumentException("No supported event was requested.");
        return result;
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RuntimeEventTraceRegistry));
    }

    private static int Clamp(int value, int minimum, int maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    internal sealed class TraceControlTarget {
        public Control Control { get; set; } = null!;
        public string ControlId { get; set; } = string.Empty;
        public string ControlName { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public string ControlPath { get; set; } = string.Empty;
    }

    private sealed class TraceSession : IDisposable {
        private readonly object _gate = new();
        private readonly string[] _eventNames;
        private readonly Queue<RuntimeEventSnapshot> _events = new();
        private readonly List<(Control Control, string EventName, Delegate Handler)> _subscriptions = new();
        private long _nextSequence = 1;
        private long _dropped;
        private int _subscribedControlCount;
        private System.Threading.Timer? _expirationTimer;
        private bool _disposed;

        public TraceSession(
            string traceId,
            DateTimeOffset startedAtUtc,
            DateTimeOffset expiresAtUtc,
            int maxEvents,
            string[] eventNames) {
            TraceId = traceId;
            StartedAtUtc = startedAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            MaxEvents = maxEvents;
            _eventNames = eventNames;
        }

        public string TraceId { get; }
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset ExpiresAtUtc { get; }
        public int MaxEvents { get; }

        public void Subscribe(TraceControlTarget target) {
            var before = _subscriptions.Count;
            foreach (var eventName in _eventNames)
                SubscribeOne(target, eventName);
            if (_subscriptions.Count > before)
                _subscribedControlCount++;
        }

        public void ScheduleExpiration(Action callback) {
            var due = ExpiresAtUtc - DateTimeOffset.UtcNow;
            _expirationTimer = new System.Threading.Timer(
                _ => callback(),
                null,
                due <= TimeSpan.Zero ? TimeSpan.Zero : due,
                Timeout.InfiniteTimeSpan);
        }

        public RuntimeEventTraceSnapshot Snapshot(
            long afterSequence = 0,
            int? maxEvents = null,
            bool? active = null) {
            lock (_gate) {
                var selected = _events
                    .Where(item => item.Sequence > afterSequence)
                    .Take(maxEvents ?? MaxEvents)
                    .Select(Clone)
                    .ToList();
                var oldestAvailable = _events.Count == 0 ? _nextSequence : _events.Peek().Sequence;
                var cursorLostEvents = _dropped > 0 && afterSequence < oldestAvailable - 1;
                var nextSequence = selected.Count == 0 ? afterSequence : selected[selected.Count - 1].Sequence;
                return new RuntimeEventTraceSnapshot {
                    TraceId = TraceId,
                    Active = active ?? (!_disposed && ExpiresAtUtc > DateTimeOffset.UtcNow),
                    StartedAtUtc = StartedAtUtc,
                    ExpiresAtUtc = ExpiresAtUtc,
                    MaxEvents = MaxEvents,
                    SubscribedControlCount = _subscribedControlCount,
                    SubscribedEvents = _eventNames.ToList(),
                    Events = selected,
                    NextSequence = nextSequence,
                    DroppedEventCount = _dropped,
                    Truncated = cursorLostEvents || selected.Count < _events.Count(item => item.Sequence > afterSequence)
                };
            }
        }

        public void Dispose() {
            lock (_gate) {
                if (_disposed)
                    return;
                _disposed = true;
                _expirationTimer?.Dispose();
                _expirationTimer = null;
                foreach (var subscription in _subscriptions)
                    RemoveHandler(subscription.Control, subscription.EventName, subscription.Handler);
                _subscriptions.Clear();
            }
        }

        private void SubscribeOne(TraceControlTarget target, string eventName) {
            EventInfo? eventInfo;
            Delegate handler;
            switch (eventName) {
                case "FormClosing" when target.Control is Form:
                    eventInfo = typeof(Form).GetEvent(nameof(Form.FormClosing));
                    handler = (FormClosingEventHandler)((_, args) => Record(target, eventName, new {
                        closeReason = args.CloseReason.ToString(),
                        cancel = args.Cancel
                    }));
                    break;
                case "Click":
                    eventInfo = typeof(Control).GetEvent(nameof(Control.Click));
                    handler = (EventHandler)((_, _) => Record(target, eventName, null));
                    break;
                case "TextChanged":
                    eventInfo = typeof(Control).GetEvent(nameof(Control.TextChanged));
                    handler = (EventHandler)((_, _) => {
                        var text = ReadText(target.Control);
                        Record(target, eventName, new { text = text.Value, textTruncated = text.Truncated });
                    });
                    break;
                case "VisibleChanged":
                    eventInfo = typeof(Control).GetEvent(nameof(Control.VisibleChanged));
                    handler = (EventHandler)((_, _) => Record(target, eventName, new { visible = target.Control.Visible }));
                    break;
                case "EnabledChanged":
                    eventInfo = typeof(Control).GetEvent(nameof(Control.EnabledChanged));
                    handler = (EventHandler)((_, _) => Record(target, eventName, new { enabled = target.Control.Enabled }));
                    break;
                case "CheckedChanged" when target.Control is CheckBox:
                    eventInfo = typeof(CheckBox).GetEvent(nameof(CheckBox.CheckedChanged));
                    handler = (EventHandler)((_, _) => Record(target, eventName, new { checkedState = ((CheckBox)target.Control).Checked }));
                    break;
                case "SelectedIndexChanged" when target.Control is ComboBox:
                    eventInfo = typeof(ComboBox).GetEvent(nameof(ComboBox.SelectedIndexChanged));
                    handler = (EventHandler)((_, _) => {
                        var text = ReadText(target.Control);
                        Record(target, eventName, new {
                            selectedIndex = ((ComboBox)target.Control).SelectedIndex,
                            text = text.Value,
                            textTruncated = text.Truncated
                        });
                    });
                    break;
                case "CheckedChanged" when target.Control is RadioButton:
                    eventInfo = typeof(RadioButton).GetEvent(nameof(RadioButton.CheckedChanged));
                    handler = (EventHandler)((_, _) => Record(target, eventName, new { checkedState = ((RadioButton)target.Control).Checked }));
                    break;
                case "SelectedIndexChanged" when target.Control is ListBox:
                    eventInfo = typeof(ListBox).GetEvent(nameof(ListBox.SelectedIndexChanged));
                    handler = (EventHandler)((_, _) => {
                        var text = ReadText(target.Control);
                        Record(target, eventName, new {
                            selectedIndex = ((ListBox)target.Control).SelectedIndex,
                            text = text.Value,
                            textTruncated = text.Truncated
                        });
                    });
                    break;
                case "SelectedIndexChanged" when target.Control is TabControl:
                    eventInfo = typeof(TabControl).GetEvent(nameof(TabControl.SelectedIndexChanged));
                    handler = (EventHandler)((_, _) => Record(target, eventName, new {
                        selectedIndex = ((TabControl)target.Control).SelectedIndex
                    }));
                    break;
                default:
                    return;
            }

            if (eventInfo?.AddMethod is null)
                return;
            try {
                eventInfo.AddEventHandler(target.Control, handler);
                _subscriptions.Add((target.Control, eventName, handler));
            }
            catch {
                // A disposed or third-party control can reject one event; keep the rest of the trace alive.
            }
        }

        private void Record(TraceControlTarget target, string eventName, object? evidence) {
            lock (_gate) {
                if (_disposed || ExpiresAtUtc <= DateTimeOffset.UtcNow)
                    return;
                var snapshot = new RuntimeEventSnapshot {
                    Sequence = _nextSequence++,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    TraceId = TraceId,
                    ControlId = target.ControlId,
                    ControlName = target.ControlName,
                    ControlType = target.ControlType,
                    ControlPath = target.ControlPath
                };
                if (evidence is not null)
                    snapshot.Evidence["state"] = JsonSerializer.SerializeToElement(evidence);
                snapshot.Evidence["event"] = JsonSerializer.SerializeToElement(eventName);
                snapshot.EventName = eventName;
                if (_events.Count >= MaxEvents) {
                    _events.Dequeue();
                    _dropped++;
                }
                _events.Enqueue(snapshot);
            }
        }

        private static void RemoveHandler(Control control, string eventName, Delegate handler) {
            try {
                var eventInfo = control switch {
                    Form when eventName == "FormClosing" => typeof(Form).GetEvent(nameof(Form.FormClosing)),
                    CheckBox when eventName == "CheckedChanged" => typeof(CheckBox).GetEvent(nameof(CheckBox.CheckedChanged)),
                    RadioButton when eventName == "CheckedChanged" => typeof(RadioButton).GetEvent(nameof(RadioButton.CheckedChanged)),
                    ComboBox when eventName == "SelectedIndexChanged" => typeof(ComboBox).GetEvent(nameof(ComboBox.SelectedIndexChanged)),
                    ListBox when eventName == "SelectedIndexChanged" => typeof(ListBox).GetEvent(nameof(ListBox.SelectedIndexChanged)),
                    TabControl when eventName == "SelectedIndexChanged" => typeof(TabControl).GetEvent(nameof(TabControl.SelectedIndexChanged)),
                    _ => typeof(Control).GetEvent(eventName)
                };
                eventInfo?.RemoveEventHandler(control, handler);
            }
            catch {
                // Disposal is best effort for controls that are already torn down.
            }
        }

        private static (string Value, bool Truncated) ReadText(Control control) {
            try {
                var value = control.Text ?? string.Empty;
                return value.Length <= MaxEvidenceTextLength
                    ? (value, false)
                    : (value.Substring(0, MaxEvidenceTextLength), true);
            }
            catch {
                return (string.Empty, false);
            }
        }

        private static RuntimeEventSnapshot Clone(RuntimeEventSnapshot source) => new() {
            Sequence = source.Sequence,
            TimestampUtc = source.TimestampUtc,
            TraceId = source.TraceId,
            ControlId = source.ControlId,
            ControlName = source.ControlName,
            ControlType = source.ControlType,
            ControlPath = source.ControlPath,
            EventName = source.EventName,
            Evidence = source.Evidence.ToDictionary(item => item.Key, item => item.Value.Clone(), StringComparer.Ordinal)
        };
    }
}