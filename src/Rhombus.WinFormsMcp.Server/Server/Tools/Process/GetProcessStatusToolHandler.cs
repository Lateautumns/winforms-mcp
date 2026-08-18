using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Process;

internal sealed class GetProcessStatusToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public GetProcessStatusToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.GetProcessStatus;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var status = _session.GetAutomation().GetProcessStatus(ToolArguments.GetInt32(arguments, "pid"));
        status["success"] = true;
        return ValueTask.FromResult(ToolJson.Result(status));
    }
}