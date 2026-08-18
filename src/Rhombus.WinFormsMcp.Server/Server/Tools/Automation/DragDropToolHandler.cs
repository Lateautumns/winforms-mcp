using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class DragDropToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public DragDropToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.DragDrop;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceId = ToolArguments.RequireString(arguments, "sourceElementId");
        var targetId = ToolArguments.RequireString(arguments, "targetElementId");
        _session.GetAutomation().DragDrop(
            ToolJson.RequireElement(_session, sourceId),
            ToolJson.RequireElement(_session, targetId));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, message = "Drag and drop completed" }));
    }
}