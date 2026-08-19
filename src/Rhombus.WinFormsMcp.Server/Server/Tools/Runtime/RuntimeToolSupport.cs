using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Runtime;

internal static class RuntimeToolSupport {
    public static int RequirePid(JsonElement arguments) {
        var pid = ToolArguments.GetInt32(arguments, "pid");
        if (pid <= 0)
            throw new ToolExecutionException("invalid_argument", "'pid' must be a positive process ID.", false);
        return pid;
    }

    public static string? GetBridgeInstanceId(JsonElement arguments) {
        if (arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !arguments.TryGetProperty("bridgeInstanceId", out var value) ||
            value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new ToolExecutionException(
                "invalid_argument",
                "'bridgeInstanceId' must be a string when provided.",
                false);

        var instanceId = value.GetString();
        if (string.IsNullOrWhiteSpace(instanceId))
            return null;
        if (instanceId.Length > 128)
            throw new ToolExecutionException(
                "invalid_argument",
                "'bridgeInstanceId' must be a non-empty string no longer than 128 characters.",
                false);
        return instanceId;
    }

    public static ToolExecutionException ToToolException(RuntimeBridgeException exception) =>
        new(exception.Code, exception.Message, exception.Retryable, exception);
}