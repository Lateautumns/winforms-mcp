using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class SelectItemToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public SelectItemToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.SelectItem;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var selectedValue = _session.GetAutomation().SelectItem(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.GetString(arguments, "value"),
            ToolArguments.GetNullableInt32(arguments, "index"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, selectedValue }));
    }
}