using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class SetTableCellToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public SetTableCellToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.SetTableCell;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var (previousValue, newValue) = _session.GetAutomation().SetTableCell(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.GetInt32(arguments, "row"),
            ToolArguments.GetInt32(arguments, "column"),
            ToolArguments.RequireString(arguments, "value"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, previousValue, newValue }));
    }
}