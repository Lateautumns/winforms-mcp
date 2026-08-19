using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;
using Rhombus.WinFormsMcp.Server.Tools.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Diagnostics;

internal sealed class StopEventTraceToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;

    public StopEventTraceToolHandler(IRuntimeBridgeClient client) => _client = client;

    public string Name => ToolNames.StopEventTrace;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        var traceId = ToolArguments.RequireString(arguments, "traceId");
        try {
            var result = await _client.StopEventTraceAsync(pid, traceId, cancellationToken).ConfigureAwait(false);
            return ToolJson.Result(new { success = true, trace = result });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}