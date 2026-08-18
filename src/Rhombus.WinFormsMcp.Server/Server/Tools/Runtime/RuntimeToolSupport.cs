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

    public static ToolExecutionException ToToolException(RuntimeBridgeException exception) =>
        new(exception.Code, exception.Message, exception.Retryable, exception);
}
