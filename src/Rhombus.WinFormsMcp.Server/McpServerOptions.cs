using Microsoft.Extensions.Logging;

namespace Rhombus.WinFormsMcp.Server;

/// <summary>
/// Strongly-typed configuration for the MCP server, bound from environment variables.
/// </summary>
public class McpServerOptions {
    public bool Headless { get; set; }
    public bool TelemetryOptOut { get; set; } = true;
    public string Tfm { get; set; } = "auto";
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    public int ToolTimeoutMs { get; set; } = 30000;
    public int RendererTimeoutMs { get; set; } = 30000;
    public int RendererStartupTimeoutMs { get; set; } = 10000;
}