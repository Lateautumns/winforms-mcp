using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Rhombus.WinFormsMcp.Server;

namespace Rhombus.WinFormsMcp.Tests;

[TestFixture]
public class McpServerOptionsTests {
    private static McpServerOptions Bind(Dictionary<string, string?> values) {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var options = new McpServerOptions();
        var configurer = new McpServerOptionsConfiguration(config);
        configurer.PostConfigure(Options.DefaultName, options);
        return options;
    }

    [Test]
    public void BindOptions_Defaults() {
        var opts = Bind(new Dictionary<string, string?>());
        Assert.That(opts.Headless, Is.False);
        Assert.That(opts.TelemetryOptOut, Is.True);
        Assert.That(opts.Tfm, Is.EqualTo("auto"));
        Assert.That(opts.MinimumLogLevel, Is.EqualTo(LogLevel.Information));
        Assert.That(opts.ToolTimeoutMs, Is.EqualTo(30000));
        Assert.That(opts.RendererTimeoutMs, Is.EqualTo(30000));
        Assert.That(opts.RendererStartupTimeoutMs, Is.EqualTo(10000));
        Assert.That(opts.RuntimeBridgeEnabled, Is.True);
        Assert.That(opts.RuntimeBridgeConnectTimeoutMs, Is.EqualTo(1000));
        Assert.That(opts.RuntimeBridgeRequestTimeoutMs, Is.EqualTo(5000));
    }

    [Test]
    public void BindOptions_HeadlessTrue() {
        var opts = Bind(new Dictionary<string, string?> { ["HEADLESS"] = "true" });
        Assert.That(opts.Headless, Is.True);
    }

    [Test]
    public void BindOptions_HeadlessOne() {
        var opts = Bind(new Dictionary<string, string?> { ["HEADLESS"] = "1" });
        Assert.That(opts.Headless, Is.True);
    }

    [Test]
    public void BindOptions_HeadlessCaseInsensitive() {
        var opts = Bind(new Dictionary<string, string?> { ["HEADLESS"] = "TRUE" });
        Assert.That(opts.Headless, Is.True);
    }

    [Test]
    public void BindOptions_TelemetryOptOut() {
        var opts = Bind(new Dictionary<string, string?> { ["TELEMETRY_OPTOUT"] = "true" });
        Assert.That(opts.TelemetryOptOut, Is.True);
    }

    [Test]
    public void BindOptions_TelemetryOptOutOne() {
        var opts = Bind(new Dictionary<string, string?> { ["TELEMETRY_OPTOUT"] = "1" });
        Assert.That(opts.TelemetryOptOut, Is.True);
    }

    [Test]
    public void BindOptions_TfmValue() {
        var opts = Bind(new Dictionary<string, string?> { ["TFM"] = "net48" });
        Assert.That(opts.Tfm, Is.EqualTo("net48"));
    }

    [Test]
    public void BindOptions_TfmWhitespace_DefaultsToAuto() {
        var opts = Bind(new Dictionary<string, string?> { ["TFM"] = "  " });
        Assert.That(opts.Tfm, Is.EqualTo("auto"));
    }

    [Test]
    public void BindOptions_TfmNull_DefaultsToAuto() {
        var opts = Bind(new Dictionary<string, string?> { ["TFM"] = null });
        Assert.That(opts.Tfm, Is.EqualTo("auto"));
    }

    [Test]
    public void BindOptions_AllSet() {
        var opts = Bind(new Dictionary<string, string?> {
            ["HEADLESS"] = "1",
            ["TELEMETRY_OPTOUT"] = "true",
            ["TFM"] = "net8.0-windows"
        });
        Assert.That(opts.Headless, Is.True);
        Assert.That(opts.TelemetryOptOut, Is.True);
        Assert.That(opts.Tfm, Is.EqualTo("net8.0-windows"));
    }

    [TestCase("Debug", LogLevel.Debug)]
    [TestCase("debug", LogLevel.Debug)]
    [TestCase("Warning", LogLevel.Warning)]
    [TestCase("", LogLevel.Information)]
    [TestCase(null, LogLevel.Information)]
    [TestCase("invalid", LogLevel.Information)]
    public void BindOptions_LogLevel(string? value, LogLevel expected) {
        var opts = Bind(new Dictionary<string, string?> { ["LOG_LEVEL"] = value });
        Assert.That(opts.MinimumLogLevel, Is.EqualTo(expected));
    }

    [Test]
    public void BindOptions_Timeouts() {
        var opts = Bind(new Dictionary<string, string?> {
            ["TOOL_TIMEOUT_MS"] = "1250",
            ["RENDERER_TIMEOUT_MS"] = "2500",
            ["RENDERER_STARTUP_TIMEOUT_MS"] = "750"
        });

        Assert.That(opts.ToolTimeoutMs, Is.EqualTo(1250));
        Assert.That(opts.RendererTimeoutMs, Is.EqualTo(2500));
        Assert.That(opts.RendererStartupTimeoutMs, Is.EqualTo(750));
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("invalid")]
    public void BindOptions_InvalidTimeoutsUseDefaults(string value) {
        var opts = Bind(new Dictionary<string, string?> {
            ["TOOL_TIMEOUT_MS"] = value,
            ["RENDERER_TIMEOUT_MS"] = value,
            ["RENDERER_STARTUP_TIMEOUT_MS"] = value
        });

        Assert.That(opts.ToolTimeoutMs, Is.EqualTo(30000));
        Assert.That(opts.RendererTimeoutMs, Is.EqualTo(30000));
        Assert.That(opts.RendererStartupTimeoutMs, Is.EqualTo(10000));
    }

    [Test]
    public void BindOptions_RuntimeBridgeSettings() {
        var opts = Bind(new Dictionary<string, string?> {
            ["RUNTIME_BRIDGE_ENABLED"] = "0",
            ["RUNTIME_BRIDGE_CONNECT_TIMEOUT_MS"] = "250",
            ["RUNTIME_BRIDGE_REQUEST_TIMEOUT_MS"] = "1750"
        });

        Assert.That(opts.RuntimeBridgeEnabled, Is.False);
        Assert.That(opts.RuntimeBridgeConnectTimeoutMs, Is.EqualTo(250));
        Assert.That(opts.RuntimeBridgeRequestTimeoutMs, Is.EqualTo(1750));
    }

    [Test]
    public void BindOptions_InvalidRuntimeBridgeTimeoutsUseDefaults() {
        var opts = Bind(new Dictionary<string, string?> {
            ["RUNTIME_BRIDGE_CONNECT_TIMEOUT_MS"] = "0",
            ["RUNTIME_BRIDGE_REQUEST_TIMEOUT_MS"] = "-1"
        });

        Assert.That(opts.RuntimeBridgeConnectTimeoutMs, Is.EqualTo(1000));
        Assert.That(opts.RuntimeBridgeRequestTimeoutMs, Is.EqualTo(5000));
    }
}
