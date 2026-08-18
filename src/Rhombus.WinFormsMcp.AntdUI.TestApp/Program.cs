using Rhombus.WinFormsMcp.RuntimeBridge;

namespace Rhombus.WinFormsMcp.AntdUI.TestApp;

internal static class Program {
    [STAThread]
    private static void Main() {
        ApplicationConfiguration.Initialize();
        var form = new AntdUiInspectionForm();
        form.Shown += (_, _) => McpRuntimeBridge.Start();
        form.FormClosed += (_, _) => McpRuntimeBridge.Stop();
        Application.Run(form);
    }
}