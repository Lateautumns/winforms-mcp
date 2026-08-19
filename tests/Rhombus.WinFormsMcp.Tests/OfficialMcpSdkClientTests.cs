using System.Text.Json;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Rhombus.WinFormsMcp.Tests;

[TestFixture]
public sealed class OfficialMcpSdkClientTests {
    [Test]
    [Timeout(60000)]
    public async Task OfficialSdkClient_InitializesAndListsRuntimeInspectionTools() {
        var serverExe = Path.Combine(TestContext.CurrentContext.TestDirectory, "winformsmcp.exe");
        Assert.That(File.Exists(serverExe), Is.True, $"Server executable was not found at {serverExe}");

        var transport = new StdioClientTransport(new StdioClientTransportOptions {
            Command = serverExe,
            Arguments = [],
            Name = "official-sdk-test",
            EnvironmentVariables = new Dictionary<string, string?> {
                ["HEADLESS"] = "true",
                ["RUNTIME_BRIDGE_ENABLED"] = "false"
            }
        }, loggerFactory: null);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions {
                ProtocolVersion = "2025-03-26",
                ClientInfo = new Implementation {
                    Name = "winforms-mcp-tests",
                    Version = "1.0.0"
                }
            },
            loggerFactory: null,
            cancellationToken: CancellationToken.None);

        var tools = await client.ListToolsAsync(
            cancellationToken: CancellationToken.None);
        var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() => {
            Assert.That(client.ServerInfo.Name, Is.EqualTo("Rhombus.WinFormsMcp"));
            Assert.That(names, Does.Contain("winforms_runtime_status"));
            Assert.That(names, Does.Contain("winforms_get_control_tree"));
            Assert.That(names, Does.Contain("winforms_inspect_control"));
            Assert.That(names, Does.Contain("winforms_get_source_mapping"));
            Assert.That(names, Does.Contain("winforms_detect_layout_issues"));
            Assert.That(names, Does.Contain("winforms_compare_screenshot"));
            Assert.That(names, Does.Contain("winforms_check_accessibility"));
            Assert.That(names, Does.Contain("winforms_start_event_trace"));
        });
    }

    [Test]
    [Timeout(60000)]
    public async Task OfficialSdkClient_ElementExistsRunsThroughUiaWorker() {
        var serverExe = Path.Combine(TestContext.CurrentContext.TestDirectory, "winformsmcp.exe");
        var workerExe = FindWorkerPath();
        var transport = new StdioClientTransport(new StdioClientTransportOptions {
            Command = serverExe,
            Arguments = [],
            Name = "official-sdk-uia-worker-test",
            EnvironmentVariables = new Dictionary<string, string?> {
                ["HEADLESS"] = "false",
                ["RUNTIME_BRIDGE_ENABLED"] = "false",
                ["UIA_WORKER_ENABLED"] = "true",
                ["UIA_WORKER_PATH"] = workerExe
            }
        }, loggerFactory: null);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions {
                ProtocolVersion = "2025-03-26",
                ClientInfo = new Implementation {
                    Name = "winforms-mcp-uia-worker-tests",
                    Version = "1.0.0"
                }
            },
            loggerFactory: null,
            cancellationToken: CancellationToken.None);

        var response = await client.CallToolAsync(
            "winforms_element_exists",
            new Dictionary<string, object?> { ["automationId"] = $"missing-{Guid.NewGuid():N}" },
            cancellationToken: CancellationToken.None);
        var text = response.Content.OfType<TextContentBlock>().Single().Text;
        using var document = JsonDocument.Parse(text);

        Assert.Multiple(() => {
            Assert.That(response.IsError, Is.Not.True);
            Assert.That(document.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(document.RootElement.GetProperty("exists").GetBoolean(), Is.False);
        });
    }

    private static string FindWorkerPath() {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current is not null) {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "Rhombus.WinFormsMcp.UiaWorker",
                "bin",
                "Release",
                "net8.0-windows",
                "Rhombus.WinFormsMcp.UiaWorker.exe");
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        Assert.Fail("The UIA Worker build output was not found.");
        return string.Empty;
    }
}