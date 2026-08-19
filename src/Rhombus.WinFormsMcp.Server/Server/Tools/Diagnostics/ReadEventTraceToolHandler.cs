using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;
using Rhombus.WinFormsMcp.Server.Tools.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Diagnostics;

internal sealed class ReadEventTraceToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;

    public ReadEventTraceToolHandler(IRuntimeBridgeClient client) => _client = client;

    public string Name => ToolNames.ReadEventTrace;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        var traceId = ToolArguments.RequireString(arguments, "traceId");
        try {
            var result = await _client.ReadEventTraceAsync(
                pid,
                traceId,
                ToolArguments.GetInt64(arguments, "afterSequence", 0),
                ToolArguments.GetInt32(arguments, "maxEvents", 200),
                cancellationToken,
                RuntimeToolSupport.GetBridgeInstanceId(arguments)).ConfigureAwait(false);
            return ToolJson.Result(new { success = true, trace = result });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}