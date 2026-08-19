using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class GetFocusedElementToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public GetFocusedElementToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.GetFocusedElement;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var focused = _session.GetAutomation().GetFocusedElement() ?? throw new ToolExecutionException(
            "element_not_found",
            "No focused element was found.");
        var requestedPid = ToolArguments.GetNullableInt32(arguments, "pid");
        var focusedPid = focused.Properties.ProcessId.ValueOrDefault;
        if (requestedPid.HasValue && requestedPid.Value != focusedPid) {
            return ValueTask.FromResult(ToolJson.Result(new {
                success = true,
                focused = false,
                message = $"Focused element belongs to process {focusedPid}, not {requestedPid.Value}."
            }));
        }

        var bounds = focused.BoundingRectangle;
        return ValueTask.FromResult(ToolJson.Result(new {
            success = true,
            focused = true,
            elementId = _session.CacheElement(focused),
            name = focused.Name,
            automationId = focused.AutomationId,
            className = focused.ClassName,
            controlType = focused.ControlType.ToString(),
            boundingRectangle = new {
                x = (int)bounds.X,
                y = (int)bounds.Y,
                width = (int)bounds.Width,
                height = (int)bounds.Height
            }
        }));
    }
}