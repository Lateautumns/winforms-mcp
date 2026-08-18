using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Runtime;

internal sealed class GetBindingsToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;

    public GetBindingsToolHandler(IRuntimeBridgeClient client) {
        _client = client;
    }

    public string Name => ToolNames.GetBindings;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        var controlId = ToolArguments.RequireString(arguments, "controlId");
        try {
            var bindings = await _client.GetBindingsAsync(pid, controlId, cancellationToken).ConfigureAwait(false);
            return ToolJson.Result(new { success = true, controlId, bindings, bindingCount = bindings.Count });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }
}