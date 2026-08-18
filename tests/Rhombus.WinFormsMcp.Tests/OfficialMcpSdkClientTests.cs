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
        });
    }
}
