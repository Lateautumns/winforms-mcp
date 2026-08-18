using System.Runtime.CompilerServices;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection;

internal sealed class ControlIdentityRegistry {
    private readonly object _gate = new();
    private readonly Dictionary<Control, string> _ids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, WeakReference<Control>> _controls = new(StringComparer.Ordinal);
    private int _nextId = 1;

    public string GetOrCreateId(Control control) {
        lock (_gate) {
            if (_ids.TryGetValue(control, out var existing))
                return existing;

            var id = $"ctrl_{_nextId++}";
            _ids[control] = id;
            _controls[id] = new WeakReference<Control>(control);
            return id;
        }
    }

    public bool TryGet(string id, out Control? control) {
        lock (_gate) {
            if (_controls.TryGetValue(id, out var reference) && reference.TryGetTarget(out control) &&
                !control.IsDisposed)
                return true;

            control = null;
            return false;
        }
    }

    public void ForgetDisposed() {
        lock (_gate) {
            foreach (var item in _controls.Where(item =>
                         !item.Value.TryGetTarget(out var control) || control.IsDisposed).ToArray()) {
                _controls.Remove(item.Key);
            }
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<Control> {
        public static readonly ReferenceEqualityComparer Instance = new();
        public bool Equals(Control? x, Control? y) => ReferenceEquals(x, y);
        public int GetHashCode(Control obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
