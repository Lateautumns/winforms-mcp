using System.Text.Json;

using FlaUI.Core.AutomationElements;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class FindElementToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public FindElementToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.FindElement;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var parent = GetParent(arguments);
        var elements = _session.GetAutomation().FindAllMatching(
            ToolArguments.GetString(arguments, "automationId"),
            ToolArguments.GetString(arguments, "name"),
            ToolArguments.GetString(arguments, "className"),
            ToolArguments.GetString(arguments, "controlType"),
            parent);
        var element = elements?.FirstOrDefault() ?? throw new ToolExecutionException(
            "element_not_found",
            "No UI element matched the supplied criteria.");
        var elementId = _session.CacheElement(element);

        return ValueTask.FromResult(ToolJson.Result(new {
            success = true,
            elementId,
            name = element.Name,
            automationId = element.AutomationId,
            className = element.ClassName,
            controlType = element.ControlType.ToString()
        }));
    }

    private AutomationElement? GetParent(JsonElement arguments) {
        var parentId = ToolArguments.GetString(arguments, "parent");
        return parentId is null ? null : ToolJson.RequireElement(_session, parentId);
    }
}