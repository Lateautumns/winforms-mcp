using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Automation.UiaWorker;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class ElementExistsToolHandler : IToolHandler {
    private readonly ISessionManager _session;
    private readonly UiaWorkerProcess _worker;

    public ElementExistsToolHandler(ISessionManager session, UiaWorkerProcess worker) {
        _session = session;
        _worker = worker;
    }

    public string Name => ToolNames.ElementExists;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var automationId = ToolArguments.RequireString(arguments, "automationId");
        var exists = await _worker.TryElementExistsAsync(automationId, 1000, cancellationToken).ConfigureAwait(false)
            ?? _session.GetAutomation().ElementExists(automationId);
        return ToolJson.Result(new { success = true, exists });
    }
}