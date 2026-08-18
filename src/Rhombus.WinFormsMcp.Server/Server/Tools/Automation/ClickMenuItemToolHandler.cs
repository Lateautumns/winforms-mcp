using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class ClickMenuItemToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public ClickMenuItemToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.ClickMenuItem;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var menuPath = ToolArguments.GetStringArray(arguments, "menuPath") ?? throw new ToolExecutionException(
            "invalid_argument",
            "'menuPath' must contain at least one menu item name.");
        _session.GetAutomation().ClickMenuItem(menuPath, ToolArguments.GetNullableInt32(arguments, "pid"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true }));
    }
}