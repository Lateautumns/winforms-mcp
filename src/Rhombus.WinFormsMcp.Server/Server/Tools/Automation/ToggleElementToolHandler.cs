using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class ToggleElementToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public ToggleElementToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.ToggleElement;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var (previousState, currentState) = _session.GetAutomation().Toggle(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.GetString(arguments, "desiredState"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, previousState, currentState }));
    }
}