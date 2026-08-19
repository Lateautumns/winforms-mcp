using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Protocol;

using Rhombus.WinFormsMcp.Server;
using Rhombus.WinFormsMcp.Server.Automation;
using Rhombus.WinFormsMcp.Server.Tools;

namespace Rhombus.WinFormsMcp.Tests;

[TestFixture]
public class FoundationRefactorTests {
    [Test]
    public void ToolRegistry_DefinitionsAndHandlersMatch() {
        var handlers = ToolDefinitionCatalog.All
            .Select(tool => (IToolHandler)new StubToolHandler(tool.Name))
            .ToArray();

        var registry = CreateRegistry(handlers);

        Assert.That(registry.Tools, Has.Count.EqualTo(46));
        Assert.That(registry.Tools.Select(tool => tool.Name), Is.Unique);
    }

    [Test]
    public void ToolRegistry_MissingHandlerFailsAtStartup() {
        var handlers = ToolDefinitionCatalog.All
            .Skip(1)
            .Select(tool => (IToolHandler)new StubToolHandler(tool.Name));

        Assert.Throws<InvalidOperationException>(() => CreateRegistry(handlers));
    }

    [Test]
    public async Task ToolRegistry_TimeoutReturnsStructuredError() {
        var handlers = ToolDefinitionCatalog.All
            .Select(tool => tool.Name == ToolNames.FindElement
                ? (IToolHandler)new BlockingToolHandler(tool.Name)
                : new StubToolHandler(tool.Name))
            .ToArray();
        var registry = CreateRegistry(handlers, timeoutMs: 20);

        var result = await registry.ExecuteAsync(
            new CallToolRequestParams {
                Name = ToolNames.FindElement,
                Arguments = new Dictionary<string, JsonElement>()
            },
            CancellationToken.None);

        Assert.That(result.IsError, Is.True);
        var content = result.Content.Single() as TextContentBlock;
        Assert.That(content, Is.Not.Null);
        using var payload = JsonDocument.Parse(content!.Text);
        Assert.That(payload.RootElement.GetProperty("error").GetProperty("code").GetString(),
            Is.EqualTo("timeout"));
    }

    [Test]
    public void ServerVersion_MatchesAssemblyVersion() {
        var informationalVersion = typeof(McpServerOptions).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Single()
            .InformationalVersion
            .Split('+')[0];

        Assert.That(ServerVersion.Current, Is.EqualTo(informationalVersion));
    }

    [Test]
    public void AutomationWait_PreCancelledTokenIsObserved() {
        using var automation = new AutomationHelper();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await automation.WaitForElementAsync(
                "missing",
                null,
                10000,
                cancellation.Token));
    }

    private static ToolRegistry CreateRegistry(
        IEnumerable<IToolHandler> handlers,
        int timeoutMs = 30000) =>
        new(
            handlers,
            new NullTelemetry(),
            Options.Create(new McpServerOptions { ToolTimeoutMs = timeoutMs }),
            NullLogger<ToolRegistry>.Instance);

    private sealed class StubToolHandler : IToolHandler {
        public StubToolHandler(string name) {
            Name = name;
        }

        public string Name { get; }

        public ValueTask<JsonElement> ExecuteAsync(
            JsonElement arguments,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(JsonSerializer.SerializeToElement(new { success = true }));
    }

    private sealed class BlockingToolHandler : IToolHandler {
        public BlockingToolHandler(string name) {
            Name = name;
        }

        public string Name { get; }

        public async ValueTask<JsonElement> ExecuteAsync(
            JsonElement arguments,
            CancellationToken cancellationToken) {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonSerializer.SerializeToElement(new { success = true });
        }
    }
}