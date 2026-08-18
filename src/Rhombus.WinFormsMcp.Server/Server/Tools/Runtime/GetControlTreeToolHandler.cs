using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Runtime;

internal sealed class GetControlTreeToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;

    public GetControlTreeToolHandler(IRuntimeBridgeClient client) {
        _client = client;
    }

    public string Name => ToolNames.GetControlTree;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        try {
            var snapshot = await _client.GetControlTreeAsync(
                pid,
                ToolArguments.GetString(arguments, "rootId"),
                ToolArguments.GetInt32(arguments, "maxDepth", 4),
                ToolArguments.GetInt32(arguments, "maxNodes", 200),
                cancellationToken).ConfigureAwait(false);
            return ToolJson.Result(new {
                success = true,
                roots = snapshot.Roots,
                nodeCount = snapshot.NodeCount,
                truncated = snapshot.Truncated,
                maxDepth = snapshot.MaxDepth,
                maxNodes = snapshot.MaxNodes
            });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}