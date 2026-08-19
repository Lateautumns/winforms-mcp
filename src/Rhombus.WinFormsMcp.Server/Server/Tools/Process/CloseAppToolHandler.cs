using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Process;

internal sealed class CloseAppToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public CloseAppToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.CloseApp;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        _session.GetAutomation().CloseApp(
            ToolArguments.GetInt32(arguments, "pid"),
            ToolArguments.GetBoolean(arguments, "force"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, message = "Application closed" }));
    }
}