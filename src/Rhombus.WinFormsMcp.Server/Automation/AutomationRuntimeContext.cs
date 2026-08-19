using FlaUI.UIA2;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Owns the UI Automation backend shared by the focused automation services.
/// </summary>
internal sealed class AutomationRuntimeContext : IDisposable {
    private UIA2Automation? _automation = new();

    public UIA2Automation Automation => _automation
        ?? throw new ObjectDisposedException(nameof(AutomationHelper));

    public UIA2Automation? AutomationOrNull => _automation;

    public void Dispose() {
        _automation?.Dispose();
        _automation = null;
    }
}