using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools;

internal static class ToolArguments {
    public static string? GetString(JsonElement arguments, string name) {
        if (arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public static string RequireString(JsonElement arguments, string name) =>
        GetString(arguments, name) ?? throw new ToolExecutionException(
            "invalid_argument",
            $"'{name}' is required.");

    public static int GetInt32(JsonElement arguments, string name, int defaultValue = 0) {
        if (arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return defaultValue;

        return arguments.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : defaultValue;
    }

    public static int? GetNullableInt32(JsonElement arguments, string name) {
        if (arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return arguments.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    public static bool GetBoolean(JsonElement arguments, string name, bool defaultValue = false) {
        if (arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !arguments.TryGetProperty(name, out var value))
            return defaultValue;

        return value.ValueKind switch {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    public static string[]? GetStringArray(JsonElement arguments, string name) {
        if (arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !arguments.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Array)
            return null;

        var result = value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
        return result.Length == 0 ? null : result;
    }

    public static int[]? GetInt32Array(JsonElement arguments, string name) {
        if (arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !arguments.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Array)
            return null;

        var result = value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out _))
            .Select(item => item.GetInt32())
            .ToArray();
        return result.Length == 0 ? null : result;
    }
}