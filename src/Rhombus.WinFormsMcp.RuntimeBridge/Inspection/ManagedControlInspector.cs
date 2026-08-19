using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeBridge.Diagnostics;
using Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;
using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection;

/// <summary>
/// Creates bounded, serializable snapshots from real WinForms controls.
/// Every public method is called by <see cref="Hosting.UiThreadDispatcher"/>.
/// </summary>
internal sealed class ManagedControlInspector : IDisposable {
    private const int DefaultSemanticMaxDepth = 4;
    private const int DefaultSemanticMaxNodes = 200;

    private static readonly string[] SafeProperties = [
        "Name", "Text", "Enabled", "Visible", "ReadOnly", "TabIndex", "Font", "ForeColor", "BackColor",
        "Dock", "Anchor", "Padding", "Margin", "AutoSize", "MinimumSize", "MaximumSize", "ClientSize",
        "Location", "Size", "Bounds"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly RuntimeBridgeOptions _options;
    private readonly string _bridgeInstanceId;
    private readonly ControlIdentityRegistry _identityRegistry = new();
    private readonly IControlProviderRegistry _providerRegistry;
    private readonly RuntimeEventTraceRegistry _eventTraces;

    public ManagedControlInspector(
        RuntimeBridgeOptions options,
        IControlProviderRegistry? providerRegistry = null,
        Action<Action>? postToUi = null,
        string? bridgeInstanceId = null) {
        _options = options;
        _bridgeInstanceId = bridgeInstanceId ?? Guid.NewGuid().ToString("N");
        _providerRegistry = providerRegistry ?? ControlProviderRegistry.CreateDefault();
        _eventTraces = new RuntimeEventTraceRegistry(options, postToUi, _bridgeInstanceId);
    }

    public BridgeHello GetHello() {
        using var process = Process.GetCurrentProcess();
        return new BridgeHello {
            ProtocolVersion = RuntimeBridgeProtocol.Version,
            Process = new RuntimeProcessInfo {
                ProcessId = process.Id,
                ProcessName = process.ProcessName ?? string.Empty,
                Runtime = RuntimeInformation.FrameworkDescription,
                Framework = "WinForms",
                BridgeVersion = _options.BridgeVersion
            },
            Capabilities = [
                "controlTree", "properties", "layout", "ancestors", "windowTree", "bindings",
                "uiThreadSnapshots", "providerSemantics", "providerSemanticPaging", "layeredWindows",
                "providerWindowMetadata", "diagnostics", "accessibility", "eventTrace"
            ]
        };
    }

    public ControlTreeSnapshot GetControlTree(
        int processId,
        string? rootId,
        int maxDepth,
        int maxNodes) {
        EnsureCurrentProcess(processId);
        var boundedDepth = Clamp(maxDepth, 0, Math.Max(0, _options.MaxDepth));
        var boundedNodes = Clamp(maxNodes, 1, Math.Max(1, _options.MaxNodes));
        _identityRegistry.ForgetDisposed();

        var snapshot = new ControlTreeSnapshot {
            MaxDepth = boundedDepth,
            MaxNodes = boundedNodes
        };
        var roots = ResolveRoots(rootId);
        foreach (var root in roots) {
            if (snapshot.NodeCount >= boundedNodes) {
                snapshot.Truncated = true;
                break;
            }

            var path = GetRootPath(root);
            snapshot.Roots.Add(BuildTreeNode(root, path, null, 0, boundedDepth, boundedNodes, snapshot));
        }

        return snapshot;
    }

    public ControlInspectionSnapshot InspectControl(
        int processId,
        string controlId,
        IReadOnlyCollection<string>? sections,
        IReadOnlyCollection<string>? includeProperties,
        ControlSemanticOptions? semanticOptions = null) {
        EnsureCurrentProcess(processId);
        var control = RequireControl(controlId);
        var requestedSections = sections is { Count: > 0 }
            ? sections.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(["identity", "state", "properties", "layout"], StringComparer.OrdinalIgnoreCase);

        var path = GetControlPath(control);
        var summary = BuildSummary(control, path, control.Parent is null ? null : GetControlId(control.Parent));
        var result = new ControlInspectionSnapshot { Summary = summary };

        if (requestedSections.Contains("state"))
            result.State = BuildState(control);
        if (requestedSections.Contains("properties"))
            result.Properties = ReadProperties(control, includeProperties);
        if (requestedSections.Contains("layout"))
            result.Layout = BuildLayout(control);
        if (requestedSections.Contains("bindings"))
            result.Bindings = ReadBindings(control);
        if (requestedSections.Contains("provider") || requestedSections.Contains("semantic")) {
            var provider = _providerRegistry.Resolve(control);
            result.Provider = DescribeProvider(provider, control);
            if (requestedSections.Contains("semantic"))
                result.Semantic = InspectProvider(provider, control, result.Provider, semanticOptions);
        }

        return result;
    }

    public List<ControlAncestorSnapshot> GetAncestors(int processId, string controlId) {
        EnsureCurrentProcess(processId);
        var control = RequireControl(controlId);
        var result = new List<ControlAncestorSnapshot>();
        var depth = 1;
        for (var parent = control.Parent; parent is not null; parent = parent.Parent) {
            result.Add(new ControlAncestorSnapshot {
                ManagedId = GetControlId(parent),
                ProcessId = _options.ProcessId,
                BridgeInstanceId = _bridgeInstanceId,
                Name = GetControlName(parent),
                Type = parent.GetType().FullName ?? parent.GetType().Name,
                ControlPath = GetControlPath(parent),
                Depth = depth++
            });
        }

        return result;
    }

    public List<WindowSnapshot> GetWindowTree(int processId, int maxNodes, int maxItems = 100) {
        EnsureCurrentProcess(processId);
        _identityRegistry.ForgetDisposed();
        var boundedItems = Clamp(maxItems, 0, Math.Max(0, _options.MaxProviderWindowItems));
        var providerMetadata = LayeredWindowInspector.InspectOpenForms(
            processId,
            _identityRegistry,
            GetControlPath,
            boundedItems,
            _bridgeInstanceId);
        return Win32WindowInspector.GetProcessWindows(
            processId,
            Clamp(maxNodes, 1, Math.Max(1, _options.MaxNodes)),
            providerMetadata);
    }

    public List<ControlBindingSnapshot> GetBindings(int processId, string controlId) {
        EnsureCurrentProcess(processId);
        return ReadBindings(RequireControl(controlId));
    }

    public RuntimeDiagnosticsSnapshot DetectDiagnostics(
        int processId,
        string? rootId,
        IReadOnlyCollection<string>? checks,
        int maxDepth,
        int maxNodes,
        int maxDiagnostics,
        CancellationToken cancellationToken) {
        EnsureCurrentProcess(processId);
        var boundedDepth = Clamp(maxDepth, 0, Math.Max(0, _options.MaxDepth));
        var boundedNodes = Clamp(maxNodes, 1, Math.Max(1, _options.MaxNodes));
        var boundedDiagnostics = Clamp(maxDiagnostics, 1, Math.Max(1, _options.MaxDiagnostics));
        var records = new List<DiagnosticControlRecord>(Math.Min(boundedNodes, 256));
        var traversalTruncated = false;
        foreach (var root in ResolveRoots(rootId)) {
            BuildDiagnosticRecords(
                root,
                null,
                0,
                boundedDepth,
                boundedNodes,
                records,
                ref traversalTruncated,
                cancellationToken);
            if (records.Count >= boundedNodes)
                break;
        }

        var result = ControlDiagnosticRules.Analyze(
            records,
            checks,
            boundedNodes,
            boundedDiagnostics,
            traversalTruncated,
            cancellationToken,
            processId,
            _bridgeInstanceId);
        result.ScannedNodes = records.Count;
        return result;
    }

    public RuntimeAccessibilitySnapshot GetAccessibility(
        int processId,
        string? rootId,
        int maxDepth,
        int maxNodes,
        int maxDiagnostics,
        CancellationToken cancellationToken) {
        EnsureCurrentProcess(processId);
        var boundedDepth = Clamp(maxDepth, 0, Math.Max(0, _options.MaxDepth));
        var boundedNodes = Clamp(maxNodes, 1, Math.Max(1, _options.MaxNodes));
        var boundedDiagnostics = Clamp(maxDiagnostics, 1, Math.Max(1, _options.MaxDiagnostics));
        var result = new RuntimeAccessibilitySnapshot {
            ProcessId = processId,
            BridgeInstanceId = _bridgeInstanceId,
            MaxNodes = boundedNodes,
            MaxDiagnostics = boundedDiagnostics
        };
        foreach (var root in ResolveRoots(rootId)) {
            BuildAccessibility(
                root,
                0,
                boundedDepth,
                boundedNodes,
                boundedDiagnostics,
                result,
                cancellationToken);
            if (result.Controls.Count >= boundedNodes)
                break;
        }
        result.ScannedNodes = result.Controls.Count;
        return result;
    }

    public RuntimeEventTraceSnapshot StartEventTrace(
        int processId,
        string? rootId,
        IReadOnlyCollection<string>? events,
        int maxEvents,
        int durationMs,
        int maxNodes,
        CancellationToken cancellationToken) {
        EnsureCurrentProcess(processId);
        var boundedNodes = Clamp(
            maxNodes,
            1,
            Math.Min(Math.Max(1, _options.MaxNodes), Math.Max(1, _options.MaxEventTraceControls)));
        var targets = new List<RuntimeEventTraceRegistry.TraceControlTarget>(boundedNodes);
        var truncated = false;
        foreach (var root in ResolveRoots(rootId)) {
            CollectTraceTargets(root, boundedNodes, targets, ref truncated, cancellationToken);
            if (targets.Count >= boundedNodes)
                break;
        }

        var snapshot = _eventTraces.Start(targets, events, maxEvents, durationMs);
        if (truncated)
            snapshot.Truncated = true;
        return snapshot;
    }

    public RuntimeEventTraceSnapshot ReadEventTrace(
        int processId,
        string traceId,
        long afterSequence,
        int maxEvents) {
        EnsureCurrentProcess(processId);
        return _eventTraces.Read(traceId, Math.Max(0, afterSequence), maxEvents);
    }

    public RuntimeEventTraceSnapshot StopEventTrace(int processId, string traceId) {
        EnsureCurrentProcess(processId);
        return _eventTraces.Stop(traceId);
    }

    public void Dispose() => _eventTraces.Dispose();

    private ControlProviderSnapshot DescribeProvider(IControlProvider provider, Control control) {
        try {
            return provider.Describe(control);
        }
        catch (Exception ex) {
            return new ControlProviderSnapshot {
                ProviderName = provider.ProviderName,
                Priority = provider.Priority,
                RuntimeType = control.GetType().FullName ?? control.GetType().Name,
                SemanticType = "unknown",
                Capabilities = [$"describe_error:{ex.GetType().Name}"]
            };
        }
    }

    private ControlSemanticSnapshot InspectProvider(
        IControlProvider provider,
        Control control,
        ControlProviderSnapshot providerSnapshot,
        ControlSemanticOptions? semanticOptions) {
        try {
            return provider.Inspect(
                control,
                CreateProviderContext(semanticOptions));
        }
        catch (Exception ex) {
            return new ControlSemanticSnapshot {
                ProviderName = provider.ProviderName,
                RuntimeType = providerSnapshot.RuntimeType,
                SemanticType = providerSnapshot.SemanticType,
                Errors = {
                    ["<provider>"] = ex.Message
                }
            };
        }
    }

    private ControlProviderContext CreateProviderContext(ControlSemanticOptions? semanticOptions) {
        var maxDepth = Clamp(
            semanticOptions?.MaxDepth ?? DefaultSemanticMaxDepth,
            0,
            Math.Max(0, _options.MaxDepth));
        var maxNodes = Clamp(
            semanticOptions?.MaxNodes ?? DefaultSemanticMaxNodes,
            1,
            Math.Max(1, _options.MaxNodes));
        return new ControlProviderContext(
            maxDepth,
            maxNodes,
            GetControlId,
            ToJsonValue,
            semanticOptions?.Start,
            semanticOptions?.Count,
            semanticOptions?.StartRow,
            semanticOptions?.RowCount,
            semanticOptions?.RowScope);
    }

    private ControlTreeNode BuildTreeNode(
        Control control,
        string path,
        string? parentId,
        int depth,
        int maxDepth,
        int maxNodes,
        ControlTreeSnapshot snapshot) {
        snapshot.NodeCount++;
        var node = new ControlTreeNode {
            Summary = BuildSummary(control, path, parentId)
        };

        var childCount = TryGet(() => control.Controls.Count, 0);
        if (depth >= maxDepth) {
            if (childCount > 0) {
                node.Truncated = true;
                snapshot.Truncated = true;
            }
            return node;
        }

        if (childCount == 0)
            return node;

        for (var index = 0; index < childCount; index++) {
            if (snapshot.NodeCount >= maxNodes) {
                node.Truncated = true;
                snapshot.Truncated = true;
                break;
            }

            var child = control.Controls[index];
            var childPath = $"{path}/{GetPathSegment(child, index)}";
            node.Children.Add(BuildTreeNode(
                child,
                childPath,
                node.Summary.Identity.ManagedId,
                depth + 1,
                maxDepth,
                maxNodes,
                snapshot));
        }

        return node;
    }

    private ControlSummary BuildSummary(Control control, string path, string? parentId) {
        var identity = new ControlIdentity {
            ManagedId = GetControlId(control),
            Hwnd = TryGetHandle(control),
            ProcessId = Process.GetCurrentProcess().Id,
            BridgeInstanceId = _bridgeInstanceId,
            ControlPath = path,
            Name = GetControlName(control),
            Type = control.GetType().FullName ?? control.GetType().Name,
            OwnerType = GetOwnerType(control),
            AutomationId = GetControlName(control)
        };
        return new ControlSummary {
            Identity = identity,
            Text = TryGet(() => control.Text, string.Empty),
            Bounds = ToRect(TryGet(() => control.Bounds, Rectangle.Empty)),
            Visible = TryGet(() => control.Visible, false),
            Enabled = TryGet(() => control.Enabled, false),
            ChildCount = TryGet(() => control.Controls.Count, 0),
            ParentId = parentId,
            Dock = TryGet(() => control.Dock.ToString(), string.Empty),
            Anchor = TryGet(() => control.Anchor.ToString(), string.Empty)
        };
    }

    private ControlStateSnapshot BuildState(Control control) => new() {
        Visible = TryGet(() => control.Visible, false),
        Enabled = TryGet(() => control.Enabled, false),
        Focused = TryGet(() => control.Focused, false),
        ReadOnly = TryGetReadOnly(control),
        Text = TryGet(() => control.Text, string.Empty)
    };

    private ControlLayoutSnapshot BuildLayout(Control control) {
        var bounds = TryGet(() => control.Bounds, Rectangle.Empty);
        var clientRectangle = TryGet(() => control.ClientRectangle, Rectangle.Empty);
        var screenBounds = TryGet(() => control.RectangleToScreen(clientRectangle), bounds);
        var parentClientSize = control.Parent is null
            ? (Size?)null
            : TryGet(() => control.Parent.ClientSize, Size.Empty);
        var dpi = TryGetDeviceDpi(control);
        return new ControlLayoutSnapshot {
            Bounds = ToRect(bounds),
            ScreenBounds = ToRect(screenBounds),
            ClientRectangle = ToRect(clientRectangle),
            Dock = TryGet(() => control.Dock.ToString(), string.Empty),
            Anchor = TryGet(() => control.Anchor.ToString(), string.Empty),
            Margin = ToThickness(TryGet(() => control.Margin, Padding.Empty)),
            Padding = ToThickness(TryGet(() => control.Padding, Padding.Empty)),
            AutoSize = TryGet(() => control.AutoSize, false),
            MinimumSize = ToSize(TryGet(() => control.MinimumSize, Size.Empty)),
            MaximumSize = ToSize(TryGet(() => control.MaximumSize, Size.Empty)),
            ClientSize = ToSize(TryGet(() => control.ClientSize, Size.Empty)),
            ParentClientSize = parentClientSize is null ? null : ToSize(parentClientSize.Value),
            DeviceDpi = dpi,
            ScaleFactor = dpi / 96d
        };
    }

    private ControlPropertiesSnapshot ReadProperties(
        Control control,
        IReadOnlyCollection<string>? includeProperties) {
        var result = new ControlPropertiesSnapshot();
        PropertyDescriptorCollection descriptors;
        try {
            descriptors = TypeDescriptor.GetProperties(control);
        }
        catch (Exception ex) {
            result.Errors["<metadata>"] = ex.Message;
            return result;
        }

        var propertyNames = SafeProperties.AsEnumerable();
        if (includeProperties is { Count: > 0 })
            propertyNames = propertyNames.Concat(includeProperties);

        foreach (var propertyName in propertyNames.Distinct(StringComparer.OrdinalIgnoreCase)) {
            var descriptor = descriptors.Find(propertyName, true);
            if (descriptor is null) {
                result.Errors[propertyName] = "Property was not found.";
                continue;
            }

            try {
                result.Values[propertyName] = ToJsonValue(descriptor.GetValue(control));
            }
            catch (Exception ex) {
                result.Errors[propertyName] = ex.Message;
            }
        }

        return result;
    }

    private static List<ControlBindingSnapshot> ReadBindings(Control control) {
        var result = new List<ControlBindingSnapshot>();
        foreach (Binding binding in control.DataBindings) {
            try {
                var propertyDescriptor = TypeDescriptor.GetProperties(control).Find(binding.PropertyName, true);
                var source = binding.DataSource is BindingSource bindingSource
                    ? bindingSource.DataSource
                    : binding.DataSource;
                var dataMemberExists = TryFindDataMember(binding.DataSource, binding.BindingMemberInfo.BindingMember);
                result.Add(new ControlBindingSnapshot {
                    Property = binding.PropertyName,
                    DataMember = binding.BindingMemberInfo.BindingMember,
                    DataSourceType = GetDataSourceType(source),
                    FormattingEnabled = binding.FormattingEnabled,
                    DataSourceUpdateMode = binding.DataSourceUpdateMode.ToString(),
                    ControlUpdateMode = binding.ControlUpdateMode.ToString(),
                    DataSourcePresent = source is not null,
                    DataMemberExists = dataMemberExists,
                    ControlPropertyExists = propertyDescriptor is not null,
                    ControlPropertyReadOnly = propertyDescriptor?.IsReadOnly
                });
            }
            catch (Exception ex) {
                result.Add(new ControlBindingSnapshot {
                    Property = binding.PropertyName,
                    DataMember = binding.BindingMemberInfo.BindingMember,
                    DataSourceType = GetDataSourceType(binding.DataSource),
                    Error = ex.Message
                });
            }
        }

        return result;
    }

    private static string? GetDataSourceType(object? dataSource) {
        if (dataSource is BindingSource bindingSource && bindingSource.DataSource is not null)
            dataSource = bindingSource.DataSource;

        if (dataSource is Type type)
            return type.FullName;
        return dataSource?.GetType().FullName;
    }

    private static bool? TryFindDataMember(object? dataSource, string? dataMember) {
        if (dataSource is null)
            return false;
        if (string.IsNullOrWhiteSpace(dataMember))
            return true;

        try {
            var segments = dataMember!.Split('.');
            PropertyDescriptorCollection descriptors;
            if (dataSource is BindingSource bindingSource)
                descriptors = bindingSource.GetItemProperties(null);
            else
                descriptors = TypeDescriptor.GetProperties(GetBoundItemType(dataSource));

            for (var index = 0; index < segments.Length; index++) {
                var descriptor = descriptors.Find(segments[index], true);
                if (descriptor is null)
                    return false;
                if (index + 1 < segments.Length)
                    descriptors = TypeDescriptor.GetProperties(descriptor.PropertyType);
            }
            return true;
        }
        catch {
            return null;
        }
    }

    private static Type GetBoundItemType(object dataSource) {
        if (dataSource is Type sourceType)
            return sourceType;

        var type = dataSource.GetType();
        var enumerableType = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerableType?.GetGenericArguments()[0] ?? type;
    }

    private void BuildDiagnosticRecords(
        Control control,
        string? parentId,
        int depth,
        int maxDepth,
        int maxNodes,
        List<DiagnosticControlRecord> records,
        ref bool traversalTruncated,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (records.Count >= maxNodes) {
            traversalTruncated = true;
            return;
        }

        var path = GetControlPath(control);
        var summary = BuildSummary(control, path, parentId);
        var layout = BuildLayout(control);
        var text = TryGet(() => control.Text, string.Empty);
        var availableWidth = Math.Max(0, layout.ClientSize.Width - layout.Padding.Left - layout.Padding.Right);
        var availableHeight = Math.Max(0, layout.ClientSize.Height - layout.Padding.Top - layout.Padding.Bottom);
        SizeSnapshot? measured = null;
        if (!string.IsNullOrEmpty(text) && CanDiagnoseTextClipping(control)) {
            try {
                var measuredSize = TextRenderer.MeasureText(
                    text,
                    control.Font,
                    Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                measured = new SizeSnapshot { Width = measuredSize.Width, Height = measuredSize.Height };
            }
            catch {
                // Text measurement is best effort; layout diagnostics still return bounds.
            }
        }

        records.Add(new DiagnosticControlRecord {
            Summary = summary,
            State = BuildState(control),
            Layout = layout,
            Bindings = ReadBindings(control),
            TabStop = TryGet(() => control.TabStop, false),
            ParentAutoScroll = control.Parent is ScrollableControl scrollable && TryGet(() => scrollable.AutoScroll, false),
            IsContainer = TryGet(() => control.Controls.Count > 0, false),
            MeasuredText = measured,
            AvailableText = measured is null ? null : new SizeSnapshot { Width = availableWidth, Height = availableHeight }
        });

        if (depth >= maxDepth) {
            if (TryGet(() => control.Controls.Count, 0) > 0)
                traversalTruncated = true;
            return;
        }

        var childCount = TryGet(() => control.Controls.Count, 0);
        for (var index = 0; index < childCount; index++) {
            if (records.Count >= maxNodes) {
                traversalTruncated = true;
                return;
            }
            BuildDiagnosticRecords(
                control.Controls[index],
                summary.Identity.ManagedId,
                depth + 1,
                maxDepth,
                maxNodes,
                records,
                ref traversalTruncated,
                cancellationToken);
        }
    }

    private void BuildAccessibility(
        Control control,
        int depth,
        int maxDepth,
        int maxNodes,
        int maxDiagnostics,
        RuntimeAccessibilitySnapshot result,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (result.Controls.Count >= maxNodes) {
            result.Truncated = true;
            return;
        }

        var summary = BuildSummary(control, GetControlPath(control), control.Parent is null ? null : GetControlId(control.Parent));
        string? accessibleName = null;
        string? accessibleDescription = null;
        try {
            var accessible = control.AccessibilityObject;
            accessibleName = accessible?.Name;
            accessibleDescription = accessible?.Description;
        }
        catch {
            // Accessibility providers can throw while controls are disposing.
        }

        var snapshot = new AccessibilityControlSnapshot {
            Summary = summary,
            AccessibleName = accessibleName,
            AccessibleDescription = accessibleDescription,
            TabIndex = TryGet(() => control.TabIndex, -1),
            TabStop = TryGet(() => control.TabStop, false),
            Focused = TryGet(() => control.Focused, false),
            Enabled = TryGet(() => control.Enabled, false),
            Visible = TryGet(() => control.Visible, false),
            AutomationId = summary.Identity.AutomationId
        };
        result.Controls.Add(snapshot);
        if (result.Diagnostics.Count < maxDiagnostics) {
            if (snapshot.Visible && snapshot.Enabled && snapshot.TabStop && string.IsNullOrWhiteSpace(snapshot.AccessibleName))
                AddAccessibility(result, "warning", "missing_accessible_name", summary.Identity.ManagedId,
                    "Visible enabled control has no accessible name.", ("name", summary.Identity.Name), ("text", summary.Text));
            if (snapshot.TabStop && snapshot.TabIndex < 0)
                AddAccessibility(result, "warning", "invalid_tab_index", summary.Identity.ManagedId,
                    "Control is a tab stop but does not expose a valid TabIndex.", ("tabStop", true), ("tabIndex", snapshot.TabIndex));
            if (!snapshot.Visible && snapshot.TabStop)
                AddAccessibility(result, "info", "hidden_tab_stop", summary.Identity.ManagedId,
                    "Hidden control remains in the keyboard tab order.", ("visible", false), ("tabStop", true));
        }
        else {
            result.Truncated = true;
        }

        if (depth >= maxDepth) {
            if (TryGet(() => control.Controls.Count, 0) > 0)
                result.Truncated = true;
            return;
        }

        var childCount = TryGet(() => control.Controls.Count, 0);
        for (var index = 0; index < childCount; index++) {
            if (result.Controls.Count >= maxNodes) {
                result.Truncated = true;
                return;
            }
            BuildAccessibility(control.Controls[index], depth + 1, maxDepth, maxNodes, maxDiagnostics, result, cancellationToken);
        }
    }

    private void CollectTraceTargets(
        Control control,
        int maxNodes,
        List<RuntimeEventTraceRegistry.TraceControlTarget> targets,
        ref bool truncated,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (targets.Count >= maxNodes) {
            truncated = true;
            return;
        }

        targets.Add(new RuntimeEventTraceRegistry.TraceControlTarget {
            Control = control,
            ControlId = GetControlId(control),
            ControlName = GetControlName(control),
            ControlType = control.GetType().FullName ?? control.GetType().Name,
            ControlPath = GetControlPath(control)
        });
        var childCount = TryGet(() => control.Controls.Count, 0);
        for (var index = 0; index < childCount; index++) {
            if (targets.Count >= maxNodes) {
                truncated = true;
                return;
            }
            CollectTraceTargets(control.Controls[index], maxNodes, targets, ref truncated, cancellationToken);
        }
    }

    private static void AddAccessibility(
        RuntimeAccessibilitySnapshot result,
        string severity,
        string code,
        string controlId,
        string message,
        params (string Name, object? Value)[] evidence) {
        if (result.Diagnostics.Count >= result.MaxDiagnostics) {
            result.Truncated = true;
            return;
        }

        var diagnostic = new DiagnosticSnapshot {
            Severity = severity,
            Code = code,
            ControlId = controlId,
            Message = message
        };
        foreach (var (name, value) in evidence)
            diagnostic.Evidence[name] = JsonSerializer.SerializeToElement(value, SerializerOptions);
        result.Diagnostics.Add(diagnostic);
    }

    private IReadOnlyList<Control> ResolveRoots(string? rootId) {
        if (!string.IsNullOrWhiteSpace(rootId))
            return [RequireControl(rootId!)];

        return Application.OpenForms.Cast<Form>()
            .Where(form => !form.IsDisposed)
            .Cast<Control>()
            .ToArray();
    }

    private Control RequireControl(string controlId) {
        if (_identityRegistry.TryGet(controlId, out var control) && control is not null)
            return control;
        throw new InvalidOperationException($"Managed control '{controlId}' was not found or has been disposed.");
    }

    private string GetControlId(Control control) => _identityRegistry.GetOrCreateId(control);

    private string GetRootPath(Control control) {
        var name = GetControlName(control);
        return string.IsNullOrWhiteSpace(name) ? control.GetType().Name : name;
    }

    private string GetControlPath(Control control) {
        var segments = new Stack<string>();
        for (var current = control; current is not null; current = current.Parent) {
            var index = current.Parent is null ? 0 : current.Parent.Controls.IndexOf(current);
            segments.Push(GetPathSegment(current, index));
        }

        return string.Join("/", segments);
    }

    private static string GetPathSegment(Control control, int index) {
        var name = GetControlName(control);
        return string.IsNullOrWhiteSpace(name) ? $"{control.GetType().Name}[{index}]" : name;
    }

    private static string GetControlName(Control control) => TryGet(() => control.Name, string.Empty);

    private static string? GetOwnerType(Control control) {
        try {
            for (Control? current = control; current is not null; current = current.Parent) {
                if (current is Form)
                    return current.GetType().FullName ?? current.GetType().Name;
            }
        }
        catch {
            // A control can be detached or disposed while a snapshot is built.
        }

        return null;
    }

    private static string? TryGetHandle(Control control) {
        try {
            return control.IsHandleCreated ? Win32WindowInspector.FormatHandle(control.Handle) : null;
        }
        catch {
            return null;
        }
    }

    private static bool TryGetReadOnly(Control control) {
        try {
            var descriptor = TypeDescriptor.GetProperties(control).Find("ReadOnly", true);
            return descriptor?.GetValue(control) is bool readOnly && readOnly;
        }
        catch {
            return false;
        }
    }

    private static int TryGetDeviceDpi(Control control) {
        try {
            var property = control.GetType().GetProperty("DeviceDpi", BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(control) is int dpi && dpi > 0 ? dpi : 96;
        }
        catch {
            return 96;
        }
    }

    private static bool CanDiagnoseTextClipping(Control control) => control switch {
        Label label => !label.AutoSize && !label.AutoEllipsis,
        ButtonBase button => !button.AutoSize,
        _ => false
    };

    private static JsonElement ToJsonValue(object? value) {
        if (value is null)
            return JsonSerializer.SerializeToElement<object?>(null, SerializerOptions);
        if (value is Color color)
            return Serialize(new { name = color.Name, argb = color.ToArgb() });
        if (value is Font font)
            return Serialize(new { family = font.FontFamily.Name, size = font.Size, style = font.Style.ToString() });
        if (value is Padding padding)
            return Serialize(new { left = padding.Left, top = padding.Top, right = padding.Right, bottom = padding.Bottom });
        if (value is Rectangle rectangle)
            return Serialize(ToRect(rectangle));
        if (value is Size size)
            return Serialize(ToSize(size));
        if (value is Point point)
            return Serialize(new PointSnapshot { X = point.X, Y = point.Y });
        if (value is Enum)
            return Serialize(value.ToString());

        var type = value.GetType();
        if (type.IsPrimitive || value is decimal || value is string)
            return Serialize(value, type);

        return Serialize(value.ToString());
    }

    private static JsonElement Serialize(object? value, Type? type = null) {
        var json = type is null
            ? JsonSerializer.Serialize(value, SerializerOptions)
            : JsonSerializer.Serialize(value, type, SerializerOptions);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static RectSnapshot ToRect(Rectangle rectangle) => new() {
        X = rectangle.X,
        Y = rectangle.Y,
        Width = rectangle.Width,
        Height = rectangle.Height
    };

    private static SizeSnapshot ToSize(Size size) => new() { Width = size.Width, Height = size.Height };

    private static ThicknessSnapshot ToThickness(Padding padding) => new() {
        Left = padding.Left,
        Top = padding.Top,
        Right = padding.Right,
        Bottom = padding.Bottom
    };

    private static T TryGet<T>(Func<T> callback, T fallback) {
        try {
            return callback();
        }
        catch {
            return fallback;
        }
    }

    private static int Clamp(int value, int minimum, int maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    private static void EnsureCurrentProcess(int processId) {
        if (processId <= 0 || processId != Process.GetCurrentProcess().Id)
            throw new InvalidOperationException(
                $"Runtime bridge belongs to process {Process.GetCurrentProcess().Id}, not {processId}.");
    }
}