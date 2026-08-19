using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;
using Rhombus.WinFormsMcp.Server.Tools.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Diagnostics;

internal sealed class StartEventTraceToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;

    public StartEventTraceToolHandler(IRuntimeBridgeClient client) => _client = client;

    public string Name => ToolNames.StartEventTrace;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        try {
            var result = await _client.StartEventTraceAsync(
                pid,
                ToolArguments.GetString(arguments, "rootId"),
                ToolArguments.GetStringArray(arguments, "events"),
                ToolArguments.GetInt32(arguments, "maxEvents", 200),
                ToolArguments.GetInt32(arguments, "durationMs", 60_000),
                ToolArguments.GetInt32(arguments, "maxNodes", 500),
                cancellationToken).ConfigureAwait(false);
            return ToolJson.Result(new { success = true, trace = result });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}