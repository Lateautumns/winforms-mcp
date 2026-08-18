using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class WaitForElementToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public WaitForElementToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.WaitForElement;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var found = await _session.GetAutomation().WaitForElementAsync(
            ToolArguments.RequireString(arguments, "automationId"),
            parent: null,
            timeoutMs: ToolArguments.GetInt32(arguments, "timeoutMs", 10000),
            cancellationToken);
        return ToolJson.Result(new { success = true, found });
    }
}