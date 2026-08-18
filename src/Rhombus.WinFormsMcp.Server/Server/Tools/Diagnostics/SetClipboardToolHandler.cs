using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Diagnostics;

internal sealed class SetClipboardToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public SetClipboardToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.SetClipboard;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        _session.GetAutomation().SetClipboardText(ToolArguments.RequireString(arguments, "text"));
        return ValueTask.FromResult(ToolJson.Result(new { success = true, message = "Clipboard text set" }));
    }
}