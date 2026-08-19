using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;
using Rhombus.WinFormsMcp.Server.Tools.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Diagnostics;

internal sealed class DetectLayoutIssuesToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;

    public DetectLayoutIssuesToolHandler(IRuntimeBridgeClient client) => _client = client;

    public string Name => ToolNames.DetectLayoutIssues;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        try {
            var result = await _client.DetectDiagnosticsAsync(
                pid,
                ToolArguments.GetString(arguments, "rootId"),
                ToolArguments.GetStringArray(arguments, "checks"),
                ToolArguments.GetInt32(arguments, "maxDepth", 6),
                ToolArguments.GetInt32(arguments, "maxNodes", 500),
                ToolArguments.GetInt32(arguments, "maxDiagnostics", 200),
                cancellationToken,
                RuntimeToolSupport.GetBridgeInstanceId(arguments)).ConfigureAwait(false);
            return ToolJson.Result(new { success = true, diagnostics = result });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}