using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Runtime;

internal sealed class GetWindowTreeToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;

    public GetWindowTreeToolHandler(IRuntimeBridgeClient client) {
        _client = client;
    }

    public string Name => ToolNames.GetWindowTree;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        try {
            var windows = await _client.GetWindowTreeAsync(
                pid,
                ToolArguments.GetInt32(arguments, "maxNodes", 200),
                cancellationToken).ConfigureAwait(false);
            return ToolJson.Result(new { success = true, windows, windowCount = windows.Count });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}