using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class GetTableDataToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public GetTableDataToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.GetTableData;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var result = _session.GetAutomation().GetTableData(
            ToolJson.RequireElement(_session, elementId),
            ToolArguments.GetInt32(arguments, "startRow"),
            ToolArguments.GetInt32(arguments, "rowCount", 50),
            ToolArguments.GetInt32Array(arguments, "columns"));
        result["success"] = true;
        return ValueTask.FromResult(ToolJson.Result(result));
    }
}