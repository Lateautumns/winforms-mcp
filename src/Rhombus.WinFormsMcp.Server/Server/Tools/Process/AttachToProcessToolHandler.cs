using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Process;

internal sealed class AttachToProcessToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public AttachToProcessToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.AttachToProcess;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var processName = ToolArguments.GetString(arguments, "processName");
        var pid = ToolArguments.GetInt32(arguments, "pid");
        if (string.IsNullOrWhiteSpace(processName) && pid <= 0)
            throw new ToolExecutionException("invalid_argument", "Either 'pid' or 'processName' is required.");

        var automation = _session.GetAutomation();
        var process = string.IsNullOrWhiteSpace(processName)
            ? automation.AttachToProcess(pid)
            : automation.AttachToProcessByName(processName);
        _session.CacheProcess(process.Id, process);

        return ValueTask.FromResult(ToolJson.Result(new {
            success = true,
            pid = process.Id,
            processName = process.ProcessName
        }));
    }
}