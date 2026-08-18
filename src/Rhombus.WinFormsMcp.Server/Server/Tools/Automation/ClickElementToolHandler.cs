using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class ClickElementToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public ClickElementToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.ClickElement;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        _session.GetAutomation().Click(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.GetBoolean(arguments, "doubleClick"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, message = "Element clicked" }));
    }
}