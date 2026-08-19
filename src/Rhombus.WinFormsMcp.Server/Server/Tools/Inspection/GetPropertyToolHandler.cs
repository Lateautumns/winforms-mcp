using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools.Inspection;

internal sealed class GetPropertyToolHandler : IToolHandler {
    private static readonly HashSet<string> SupportedProperties = new(StringComparer.OrdinalIgnoreCase) {
        "name", "automationId", "className", "controlType", "isOffscreen", "isEnabled",
        "value", "text", "isChecked", "toggleState", "isSelected", "selectedItem",
        "items", "itemCount", "boundingRectangle", "isExpanded", "min", "max", "current"
    };

    private readonly ISessionManager _session;

    public GetPropertyToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.GetProperty;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var elementId = ToolArguments.RequireString(arguments, "elementId");
        var propertyName = ToolArguments.RequireString(arguments, "propertyName");
        if (!SupportedProperties.Contains(propertyName)) {
            throw new ToolExecutionException(
                "unsupported_property",
                $"Unknown property '{propertyName}'. Supported properties: {string.Join(", ", SupportedProperties)}.");
        }

        var value = _session.GetAutomation().GetProperty(ToolJson.RequireElement(_session, elementId), propertyName);
        return ValueTask.FromResult(ToolJson.Result(new {
            success = true,
            propertyName,
            value = value?.ToString()
        }));
    }
}