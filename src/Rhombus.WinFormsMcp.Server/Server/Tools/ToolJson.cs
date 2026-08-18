using System.Text.Json;

using FlaUI.Core.AutomationElements;

namespace Rhombus.WinFormsMcp.Server.Tools;

internal static class ToolJson {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement Result(object value) =>
        JsonSerializer.SerializeToElement(value, SerializerOptions);

    public static AutomationElement RequireElement(ISessionManager session, string elementId) =>
        session.GetElement(elementId) ?? throw new ToolExecutionException(
            "element_not_found",
            $"Element '{elementId}' was not found in the current session.");
}