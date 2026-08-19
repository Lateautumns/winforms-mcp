using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class RaiseEventToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public RaiseEventToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.RaiseEvent;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var result = _session.GetAutomation().RaiseEvent(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.RequireString(arguments, "eventName"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, result }));
    }
}