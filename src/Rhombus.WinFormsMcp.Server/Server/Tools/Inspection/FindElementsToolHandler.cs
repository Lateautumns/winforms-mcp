using System.Text.Json;

using FlaUI.Core.AutomationElements;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class FindElementsToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public FindElementsToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.FindElements;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var parent = GetParent(arguments);
        var elements = _session.GetAutomation().FindAllMatching(
            ToolArguments.GetString(arguments, "automationId"),
            ToolArguments.GetString(arguments, "name"),
            ToolArguments.GetString(arguments, "className"),
            ToolArguments.GetString(arguments, "controlType"),
            parent) ?? [];

        var results = elements.Select(element => new {
            elementId = _session.CacheElement(element),
            name = element.Name,
            automationId = element.AutomationId,
            className = element.ClassName,
            controlType = element.ControlType.ToString()
        }).ToArray();
        return ValueTask.FromResult(ToolJson.Result(new { success = true, count = results.Length, elements = results }));
    }

    private AutomationElement? GetParent(JsonElement arguments) {
        var parentId = ToolArguments.GetString(arguments, "parent");
        return parentId is null ? null : ToolJson.RequireElement(_session, parentId);
    }
}