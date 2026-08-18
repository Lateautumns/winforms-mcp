using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class WaitForConditionToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public WaitForConditionToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.WaitForCondition;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var (matched, actualValue, elapsedMs) = await _session.GetAutomation().WaitForConditionAsync(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.RequireString(arguments, "propertyName"),
            ToolArguments.RequireString(arguments, "expectedValue"),
            ToolArguments.GetString(arguments, "comparison") ?? "equals",
            ToolArguments.GetInt32(arguments, "timeoutMs", 10000),
            cancellationToken);
        return ToolJson.Result(new { success = true, matched, actualValue, elapsedMs });
    }
}