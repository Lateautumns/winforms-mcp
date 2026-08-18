using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class SetValueToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public SetValueToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.SetValue;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        _session.GetAutomation().SetValue(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.RequireString(arguments, "value"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, message = "Value set" }));
    }
}