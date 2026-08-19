using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class ScrollElementToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public ScrollElementToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.ScrollElement;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var result = _session.GetAutomation().Scroll(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.RequireString(arguments, "direction"),
            ToolArguments.GetInt32(arguments, "amount", 1),
            ToolArguments.GetString(arguments, "scrollType") ?? "line");
        result["success"] = true;
        return ValueTask.FromResult(ToolJson.Result(result));
    }
}