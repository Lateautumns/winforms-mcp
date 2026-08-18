using System.Text.Json;

using FlaUI.Core.AutomationElements;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class ListenForEventToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public ListenForEventToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.ListenForEvent;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var elementId = ToolArguments.GetString(arguments, "elementId");
        AutomationElement? element = elementId is null ? null : ToolJson.RequireElement(_session, elementId);
        var (fired, eventDetails, elapsedMs) = await _session.GetAutomation().ListenForEventAsync(
            element,
            ToolArguments.RequireString(arguments, "eventType"),
            ToolArguments.GetInt32(arguments, "timeoutMs", 10000),
            cancellationToken);
        return ToolJson.Result(new { success = true, fired, eventDetails, elapsedMs });
    }
}