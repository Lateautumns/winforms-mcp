using System.Text.Json;

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
        try {
            var snapshot = await _client.InspectControlAsync(
                pid,
                controlId,
                ToolArguments.GetStringArray(arguments, "sections"),
                ToolArguments.GetStringArray(arguments, "includeProperties"),
                cancellationToken).ConfigureAwait(false);
            snapshot.Correlation = _correlation.TryCorrelate(snapshot.Summary.Identity);
            return ToolJson.Result(new {
                success = true,
                identity = snapshot.Summary.Identity,
                summary = snapshot.Summary,
                state = snapshot.State,
                properties = snapshot.Properties,
                layout = snapshot.Layout,
                bindings = snapshot.Bindings,
                correlation = snapshot.Correlation
            });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}
