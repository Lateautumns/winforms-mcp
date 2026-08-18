using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class ReadTooltipToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public ReadTooltipToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.ReadTooltip;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var tooltip = _session.GetAutomation().GetTooltipText(ToolJson.RequireElement(_session, elementId));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, tooltip }));
    }
}