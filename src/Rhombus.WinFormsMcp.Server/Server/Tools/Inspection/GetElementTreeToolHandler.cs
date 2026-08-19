using System.Text.Json;

using FlaUI.Core.AutomationElements;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class GetElementTreeToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public GetElementTreeToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.GetElementTree;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var automation = _session.GetAutomation();
        var elementId = ToolArguments.GetString(arguments, "elementId");
        AutomationElement? root = elementId is null
            ? automation.GetMainWindow(ToolArguments.GetInt32(arguments, "pid"))
            : ToolJson.RequireElement(_session, elementId);
        if (root is null)
            throw new ToolExecutionException("window_not_found", "A valid 'pid' or 'elementId' root is required.");

        var tree = automation.GetElementTree(
            root,
            ToolArguments.GetInt32(arguments, "depth", 3),
            ToolArguments.GetInt32(arguments, "maxElements", 50),
            _session.CacheElement);
        return ValueTask.FromResult(ToolJson.Result(new { success = true, tree, elementCount = CountNodes(tree) }));
    }

    private static int CountNodes(IEnumerable<Dictionary<string, object?>> nodes) {
        var count = 0;
        foreach (var node in nodes) {
            count++;
            if (node.TryGetValue("children", out var children) && children is IEnumerable<Dictionary<string, object?>> childNodes)
                count += CountNodes(childNodes);
        }
        return count;
    }
}