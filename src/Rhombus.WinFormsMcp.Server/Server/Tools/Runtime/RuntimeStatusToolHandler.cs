using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Runtime;

internal sealed class RuntimeStatusToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;

    public RuntimeStatusToolHandler(IRuntimeBridgeClient client) {
        _client = client;
    }

    public string Name => ToolNames.RuntimeStatus;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        var status = await _client.GetStatusAsync(pid, cancellationToken).ConfigureAwait(false);
        return ToolJson.Result(new { success = true, status });
    }
}