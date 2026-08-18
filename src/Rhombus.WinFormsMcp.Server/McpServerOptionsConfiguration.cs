using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Rhombus.WinFormsMcp.Server;

/// <summary>
/// Configures McpServerOptions with custom parsing for boolean and LogLevel values.
/// </summary>
internal class McpServerOptionsConfiguration : IPostConfigureOptions<McpServerOptions> {
    private readonly IConfiguration _configuration;

    public McpServerOptionsConfiguration(IConfiguration configuration) {
        _configuration = configuration;
    }

    public void PostConfigure(string? name, McpServerOptions options) {
        // Custom boolean parsing: accepts "true", "1" (case-insensitive), or false/empty
        var headlessValue = _configuration["HEADLESS"];
        if (!string.IsNullOrWhiteSpace(headlessValue)) {
            options.Headless = string.Equals(headlessValue, "true", StringComparison.OrdinalIgnoreCase) || headlessValue == "1";
        }

        var telemetryOptOutValue = _configuration["TELEMETRY_OPTOUT"];
        if (!string.IsNullOrWhiteSpace(telemetryOptOutValue)) {
            options.TelemetryOptOut = string.Equals(telemetryOptOutValue, "true", StringComparison.OrdinalIgnoreCase) || telemetryOptOutValue == "1";
        }

        // Custom TFM handling: default to "auto" if not set
        var tfm = _configuration["TFM"];
        if (!string.IsNullOrWhiteSpace(tfm)) {
            options.Tfm = tfm.Trim();
        }

        // Custom LogLevel parsing: parse from LOG_LEVEL env var
        var logLevelValue = _configuration["LOG_LEVEL"];
        if (!string.IsNullOrWhiteSpace(logLevelValue)) {
            if (Enum.TryParse<LogLevel>(logLevelValue, ignoreCase: true, out var level)) {
                options.MinimumLogLevel = level;
            }
        }

        options.ToolTimeoutMs = ParsePositiveInt("TOOL_TIMEOUT_MS", options.ToolTimeoutMs);
        options.RendererTimeoutMs = ParsePositiveInt("RENDERER_TIMEOUT_MS", options.RendererTimeoutMs);
        options.RendererStartupTimeoutMs = ParsePositiveInt("RENDERER_STARTUP_TIMEOUT_MS", options.RendererStartupTimeoutMs);
    }

    private int ParsePositiveInt(string key, int defaultValue) {
        var value = _configuration[key];
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }
}