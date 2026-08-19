using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class ListWindowsToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public ListWindowsToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.ListWindows;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var windows = _session.GetAutomation().ListWindows(ToolArguments.GetInt32(arguments, "pid"));
        for (var index = 0; index < windows.Count; index++)
            windows[index]["windowIndex"] = index;

        return ValueTask.FromResult(ToolJson.Result(new {
            success = true,
            windowCount = windows.Count,
            windows
        }));
    }
}