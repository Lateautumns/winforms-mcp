using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class TypeTextToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public TypeTextToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.TypeText;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        _session.GetAutomation().TypeText(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.RequireString(arguments, "text"),
            ToolArguments.GetBoolean(arguments, "clearFirst"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, message = "Text typed" }));
    }
}