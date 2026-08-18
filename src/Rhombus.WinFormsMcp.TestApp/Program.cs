using Rhombus.WinFormsMcp.RuntimeBridge;

namespace Rhombus.WinFormsMcp.TestApp;

static class Program {
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main() {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        var form = new Form1();
        form.Shown += (_, _) => McpRuntimeBridge.Start();
        form.FormClosed += (_, _) => McpRuntimeBridge.Stop();
        Application.Run(form);
    }
}
