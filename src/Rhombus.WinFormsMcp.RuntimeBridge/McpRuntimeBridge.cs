using System.Windows.Forms;

namespace Rhombus.WinFormsMcp.RuntimeBridge;

/// <summary>
/// Entry point embedded by a target WinForms application.
/// Call <c>McpRuntimeBridge.Start()</c> from the application's UI startup path.
/// </summary>
public static class McpRuntimeBridge {
    private static readonly object Gate = new();
    private static RuntimeBridgeHost? _current;

    public static RuntimeBridgeHost Start(RuntimeBridgeOptions? options = null) {
        lock (Gate) {
            if (_current is { IsRunning: true })
                return _current;

            var invoker = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
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