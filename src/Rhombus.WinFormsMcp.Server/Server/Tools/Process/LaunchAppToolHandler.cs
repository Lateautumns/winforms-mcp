using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Process;

internal sealed class LaunchAppToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public LaunchAppToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.LaunchApp;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ToolArguments.RequireString(arguments, "path");
        var process = _session.GetAutomation().LaunchApp(
            path,
            ToolArguments.GetString(arguments, "arguments"),
            ToolArguments.GetString(arguments, "workingDirectory"));
        _session.CacheProcess(process.Id, process);

        return ValueTask.FromResult(ToolJson.Result(new {
            success = true,
            pid = process.Id,
            processName = process.ProcessName
        }));
    }
}