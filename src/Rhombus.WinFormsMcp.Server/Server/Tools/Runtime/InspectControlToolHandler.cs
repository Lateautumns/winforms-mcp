using System.Text.Json;

using Rhombus.WinFormsMcp.RuntimeContracts;
using Rhombus.WinFormsMcp.Server.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Runtime;

internal sealed class InspectControlToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;
    private readonly ManagedUiaCorrelationService _correlation;

    public InspectControlToolHandler(
        IRuntimeBridgeClient client,
        ManagedUiaCorrelationService correlation) {
        _client = client;
        _correlation = correlation;
    }

    public string Name => ToolNames.InspectControl;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        var controlId = ToolArguments.RequireString(arguments, "controlId");
        var semanticOptions = CreateSemanticOptions(arguments);
        try {
            var snapshot = await _client.InspectControlAsync(
                pid,
                controlId,
                ToolArguments.GetStringArray(arguments, "sections"),
                ToolArguments.GetStringArray(arguments, "includeProperties"),
                cancellationToken,
                semanticOptions,
                RuntimeToolSupport.GetBridgeInstanceId(arguments)).ConfigureAwait(false);
            snapshot.Correlation = _correlation.TryCorrelate(snapshot.Summary.Identity);
            var result = new Dictionary<string, object?> {
                ["success"] = true,
                ["identity"] = snapshot.Summary.Identity,
                ["summary"] = snapshot.Summary,
                ["state"] = snapshot.State,
                ["properties"] = snapshot.Properties,
                ["layout"] = snapshot.Layout,
                ["bindings"] = snapshot.Bindings,
                ["correlation"] = snapshot.Correlation
            };

            if (snapshot.Provider is not null)
                result["provider"] = snapshot.Provider;
            if (snapshot.Semantic is not null)
                result["semantic"] = snapshot.Semantic;

            return ToolJson.Result(result);
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }

    private static ControlSemanticOptions? CreateSemanticOptions(JsonElement arguments) {
        var options = new ControlSemanticOptions {
            MaxDepth = ToolArguments.GetNullableInt32(arguments, "maxDepth"),
            MaxNodes = ToolArguments.GetNullableInt32(arguments, "maxNodes"),
            Start = ToolArguments.GetNullableInt32(arguments, "start"),
            Count = ToolArguments.GetNullableInt32(arguments, "count"),
            StartRow = ToolArguments.GetNullableInt32(arguments, "startRow"),
            RowCount = ToolArguments.GetNullableInt32(arguments, "rowCount"),
            RowScope = ToolArguments.GetString(arguments, "rowScope")
        };

        return options.MaxDepth.HasValue ||
            options.MaxNodes.HasValue ||
            options.Start.HasValue ||
            options.Count.HasValue ||
            options.StartRow.HasValue ||
            options.RowCount.HasValue ||
            options.RowScope is not null
            ? options
            : null;
    }
}