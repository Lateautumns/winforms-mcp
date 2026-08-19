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
    public bool RuntimeBridgeEnabled { get; set; } = true;
    public int RuntimeBridgeConnectTimeoutMs { get; set; } = 1000;
    public int RuntimeBridgeRequestTimeoutMs { get; set; } = 5000;
    public bool UiaWorkerEnabled { get; set; } = true;
    public string? UiaWorkerPath { get; set; }
    public int UiaWorkerStartupTimeoutMs { get; set; } = 5000;
    public int UiaWorkerRequestTimeoutMs { get; set; } = 15000;
    public int UiaWorkerMaxResponseBytes { get; set; } = 1_048_576;
}