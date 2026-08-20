using System.Windows.Forms;

namespace Rhombus.WinFormsMcp.RuntimeBridge;

/// <summary>
/// Entry point embedded by a target WinForms application.
/// Call <c>McpRuntimeBridge.StartForControl(form)</c> from <c>Form.Shown</c>,
/// or keep using <c>McpRuntimeBridge.Start()</c> when a WinForms UI thread is
/// already available.
/// </summary>
public static class McpRuntimeBridge {
    private static readonly object Gate = new();
    private static RuntimeBridgeHost? _current;

    /// <summary>
    /// Starts the bridge bound to a specific control. The control must be alive
    /// and have a window handle, so this is normally called from
    /// <see cref="Form.Shown"/> (or after the handle has been created) on the
    /// UI thread. Every control read is marshalled to the control's UI thread.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="invoker"/> is null.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="invoker"/> is disposed or is being disposed.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="invoker"/> has no window handle yet.</exception>
    public static RuntimeBridgeHost StartForControl(Control invoker, RuntimeBridgeOptions? options = null) {
        if (invoker is null)
            throw new ArgumentNullException(nameof(invoker));
        if (invoker.IsDisposed || invoker.Disposing)
            throw new ObjectDisposedException(
                nameof(invoker),
                "The control bound to RuntimeBridge is disposed or is being disposed.");
        if (!invoker.IsHandleCreated)
            throw new InvalidOperationException(
                "The control bound to RuntimeBridge has no window handle yet. Call McpRuntimeBridge.StartForControl(form) from Form.Shown (or after the handle has been created) so the bridge can dispatch to the UI thread.");

        lock (Gate) {
            if (_current is { IsRunning: true })
                return _current;

            _current = new RuntimeBridgeHost(options ?? new RuntimeBridgeOptions(), invoker);
            _current.Start();
            return _current;
        }
    }

    /// <summary>
    /// Starts the bridge with a UI dispatch target derived from the current
    /// state: the first open form when one exists, otherwise the current
    /// WinForms UI synchronization context. When neither is available the call
    /// fails immediately instead of falling back to cross-thread control access.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No open form and no WinForms UI synchronization context is available.
    /// </exception>
    public static RuntimeBridgeHost Start(RuntimeBridgeOptions? options = null) {
        lock (Gate) {
            if (_current is { IsRunning: true })
                return _current;

            var invoker = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (invoker is null && SynchronizationContext.Current is not WindowsFormsSynchronizationContext) {
                throw new InvalidOperationException(
                    "No WinForms UI dispatch target is available. Start the bridge from the WinForms UI thread, or migrate to an explicit control binding: protected override void OnShown(EventArgs e) { McpRuntimeBridge.StartForControl(this); }");
            }

            _current = new RuntimeBridgeHost(options ?? new RuntimeBridgeOptions(), invoker);
            _current.Start();
            return _current;
        }
    }

    public static void Stop() {
        StopAsync().GetAwaiter().GetResult();
    }

    public static async Task StopAsync() {
        RuntimeBridgeHost? current;
        lock (Gate) {
            current = _current;
            _current = null;
        }

        if (current is not null)
            await current.StopAsync().ConfigureAwait(false);
    }
}