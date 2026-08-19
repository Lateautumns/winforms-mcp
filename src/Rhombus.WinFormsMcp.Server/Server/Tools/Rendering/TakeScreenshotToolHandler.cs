using System.Text.Json;

using FlaUI.Core.AutomationElements;

namespace Rhombus.WinFormsMcp.Server.Tools.Rendering;

internal sealed class TakeScreenshotToolHandler : IToolHandler {
    private readonly ISessionManager _session;

    public TakeScreenshotToolHandler(ISessionManager session) {
        _session = session;
    }

    public string Name => ToolNames.TakeScreenshot;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var outputPath = ToolArguments.GetString(arguments, "outputPath");
        var elementId = ToolArguments.GetString(arguments, "elementId");
        AutomationElement? element = elementId is null ? null : ToolJson.RequireElement(_session, elementId);
        var useTemporaryFile = string.IsNullOrWhiteSpace(outputPath);
        var screenshotPath = useTemporaryFile
            ? Path.Combine(Path.GetTempPath(), $"winforms-mcp-screenshot-{Guid.NewGuid():N}.png")
            : outputPath!;

        try {
            cancellationToken.ThrowIfCancellationRequested();
            _session.GetAutomation().TakeScreenshot(
                screenshotPath,
                element,
                ToolArguments.GetNullableInt32(arguments, "pid"));
            var bytes = await File.ReadAllBytesAsync(screenshotPath, cancellationToken);
            return ToolJson.Result(new { success = true, imageBase64 = Convert.ToBase64String(bytes) });
        }
        finally {
            if (useTemporaryFile) {
                try {
                    File.Delete(screenshotPath);
                }
                catch (IOException) {
                    // Best-effort cleanup; the OS can still hold the image briefly.
                }
            }
        }
    }
}