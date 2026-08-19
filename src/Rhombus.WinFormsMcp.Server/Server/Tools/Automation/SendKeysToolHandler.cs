using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class SendKeysToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public SendKeysToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.SendKeys;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        _session.GetAutomation().SendKeys(
            ToolArguments.RequireString(arguments, "keys"),
            ToolArguments.GetNullableInt32(arguments, "pid"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, message = "Keys sent" }));
    }
}