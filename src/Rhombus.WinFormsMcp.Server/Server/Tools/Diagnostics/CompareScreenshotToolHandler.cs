using System.Text.Json;

using Rhombus.WinFormsMcp.Server.Diagnostics;

namespace Rhombus.WinFormsMcp.Server.Tools.Diagnostics;

internal sealed class CompareScreenshotToolHandler : IToolHandler {
    private readonly ScreenshotDiffService _diff;

    public CompareScreenshotToolHandler(ScreenshotDiffService diff) => _diff = diff;

    public string Name => ToolNames.CompareScreenshot;

    public ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var beforePath = ToolArguments.GetString(arguments, "beforePath");
        var afterPath = ToolArguments.GetString(arguments, "afterPath");
        var beforeBase64 = ToolArguments.GetString(arguments, "beforeBase64");
        var afterBase64 = ToolArguments.GetString(arguments, "afterBase64");
        if ((string.IsNullOrWhiteSpace(beforePath) && string.IsNullOrWhiteSpace(beforeBase64)) ||
            (string.IsNullOrWhiteSpace(afterPath) && string.IsNullOrWhiteSpace(afterBase64)))
            throw new ToolExecutionException("invalid_argument", "Each screenshot must provide a path or base64 payload.", false);
        var result = _diff.Compare(
            beforePath,
            afterPath,
            beforeBase64,
            afterBase64,
            ToolArguments.GetInt32(arguments, "maxRegions", 100),
            ToolArguments.GetInt32(arguments, "pixelThreshold", 0),
            cancellationToken);
        return ValueTask.FromResult(ToolJson.Result(new { success = true, diff = result }));
    }
}