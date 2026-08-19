using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Runtime;

internal sealed class GetSourceMappingToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;
    private readonly SourceMappingService _sourceMapping;

    public GetSourceMappingToolHandler(
        IRuntimeBridgeClient client,
        SourceMappingService sourceMapping) {
        _client = client;
        _sourceMapping = sourceMapping;
    }

    public string Name => ToolNames.GetSourceMapping;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        var controlId = ToolArguments.RequireString(arguments, "controlId");
        try {
            var inspection = await _client.InspectControlAsync(
                pid,
                controlId,
                ["identity"],
                null,
                cancellationToken,
                bridgeInstanceId: RuntimeToolSupport.GetBridgeInstanceId(arguments)).ConfigureAwait(false);
            var mapping = await _sourceMapping.MapAsync(
                pid,
                inspection.Summary.Identity,
                ToolArguments.GetString(arguments, "sourceRoot"),
                cancellationToken,
                ToolArguments.GetNullableInt32(arguments, "maxFiles")).ConfigureAwait(false);
            return ToolJson.Result(new { success = true, mapping });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}