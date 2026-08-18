using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Automation;

internal sealed class ManageWindowToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public ManageWindowToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.ManageWindow;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _session.GetAutomation().ManageWindow(
            ToolArguments.GetInt32(arguments, "pid"),
            ToolArguments.RequireString(arguments, "action"),
            ToolArguments.GetNullableInt32(arguments, "width"),
            ToolArguments.GetNullableInt32(arguments, "height"),
            ToolArguments.GetNullableInt32(arguments, "x"),
            ToolArguments.GetNullableInt32(arguments, "y"));
        result["success"] = true;
        return ValueTask.FromResult(ToolJson.Result(result));
    }
}