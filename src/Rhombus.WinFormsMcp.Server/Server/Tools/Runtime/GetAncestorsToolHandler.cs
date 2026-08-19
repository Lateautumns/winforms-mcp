using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Runtime;

internal sealed class GetAncestorsToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;

    public GetAncestorsToolHandler(IRuntimeBridgeClient client) {
        _client = client;
    }

    public string Name => ToolNames.GetAncestors;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        var controlId = ToolArguments.RequireString(arguments, "controlId");
        try {
            var ancestors = await _client.GetAncestorsAsync(
                pid,
                controlId,
                cancellationToken,
                RuntimeToolSupport.GetBridgeInstanceId(arguments)).ConfigureAwait(false);
            return ToolJson.Result(new { success = true, controlId, ancestors });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}