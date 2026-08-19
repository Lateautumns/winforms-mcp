using Rhombus.WinFormsMcp.RuntimeBridge;

namespace Rhombus.WinFormsMcp.AntdUI.TestApp;

internal static class Program {
    [STAThread]
    private static void Main() {
        ApplicationConfiguration.Initialize();
        var form = new AntdUiInspectionForm();
        form.Shown += (_, _) => {
            McpRuntimeBridge.Start();
            var popup = Environment.GetEnvironmentVariable("WINFORMS_MCP_OPEN_ANTDUI_POPUP");
            if (string.Equals(popup, "select", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(popup, "1", StringComparison.Ordinal))
                form.BeginInvoke(form.OpenInspectionSelectDropdown);
            else if (string.Equals(popup, "tooltip", StringComparison.OrdinalIgnoreCase))
                form.BeginInvoke(form.OpenInspectionTooltip);
            else if (string.Equals(popup, "menu", StringComparison.OrdinalIgnoreCase))
                form.BeginInvoke(form.OpenInspectionMenuPopup);
            else if (string.Equals(popup, "message", StringComparison.OrdinalIgnoreCase))
                form.BeginInvoke(form.OpenInspectionMessage);
            else if (string.Equals(popup, "drawer", StringComparison.OrdinalIgnoreCase))
                form.BeginInvoke(form.OpenInspectionDrawer);
        };
        form.FormClosed += (_, _) => McpRuntimeBridge.Stop();
        Application.Run(form);
    }
}