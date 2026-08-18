using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Diagnostics;

internal sealed class GetClipboardToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public GetClipboardToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.GetClipboard;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ToolJson.Result(new {
            success = true,
            text = _session.GetAutomation().GetClipboardText()
        }));
    }
}