using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Automation.UiaWorker;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class WaitForElementToolHandler : IToolHandler {
    private readonly ISessionManager _session;
    private readonly UiaWorkerProcess _worker;

    public WaitForElementToolHandler(ISessionManager session, UiaWorkerProcess worker) {
        _session = session;
        _worker = worker;
    }

    public string Name => ToolNames.WaitForElement;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var automationId = ToolArguments.RequireString(arguments, "automationId");
        var timeoutMs = ToolArguments.GetInt32(arguments, "timeoutMs", 10000);
        var found = await _worker.TryElementExistsAsync(automationId, timeoutMs, cancellationToken).ConfigureAwait(false)
            ?? await _session.GetAutomation().WaitForElementAsync(
                automationId,
                parent: null,
                timeoutMs,
                cancellationToken);
        return ToolJson.Result(new { success = true, found });
    }
}