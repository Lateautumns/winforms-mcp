using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class OpenContextMenuToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public OpenContextMenuToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.OpenContextMenu;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var menu = _session.GetAutomation().OpenContextMenu(ToolJson.RequireElement(_session, elementId))
            ?? throw new ToolExecutionException("element_not_found", "The context menu did not appear.");
        return ValueTask.FromResult(ToolJson.Result(new {
            success = true,
            menuElementId = _session.CacheElement(menu),
            message = "Context menu opened. Use click_menu_item or find_element with this menuElementId as parent."
        }));
    }
}