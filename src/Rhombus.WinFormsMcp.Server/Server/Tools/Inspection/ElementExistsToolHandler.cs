using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class ElementExistsToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public ElementExistsToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.ElementExists;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = _session.GetAutomation().ElementExists(ToolArguments.RequireString(arguments, "automationId"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, exists }));
    }
}