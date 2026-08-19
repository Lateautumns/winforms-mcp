using System.Text.Json;

using Rhombus.WinFormsMcp.RuntimeContracts;
using Rhombus.WinFormsMcp.Server.Runtime;
using Rhombus.WinFormsMcp.Server.Tools;
using Rhombus.WinFormsMcp.Server.Tools.Runtime;

namespace Rhombus.WinFormsMcp.Tests.Runtime;

[TestFixture]
public sealed class SourceIndexTests {
    [Test]
    public void SourceIdentitySnapshot_OmitsUnavailableOptionalFields() {
        var json = JsonSerializer.Serialize(
            new SourceMappingSnapshot(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.That(json, Does.Not.Contain("\"source\":null"));
    }

    [Test]
    public void SourceMappingToolSchema_ExposesOptionalMaxFiles() {
        var tool = ToolDefinitionCatalog.All.Single(definition => definition.Name == ToolNames.GetSourceMapping);
        var properties = tool.InputSchema.GetProperty("properties");
        var required = tool.InputSchema.GetProperty("required");

        Assert.Multiple(() => {
            Assert.That(properties.TryGetProperty("maxFiles", out _), Is.True);
            Assert.That(required.EnumerateArray().Select(value => value.GetString()), Does.Not.Contain("maxFiles"));
        });
    }

    [Test]
    public void RuntimeToolSchemas_ExposeOptionalBridgeInstanceId() {
        var runtimeToolNames = new[] {
            ToolNames.GetControlTree,
            ToolNames.InspectControl,
            ToolNames.GetAncestors,
            ToolNames.GetWindowTree,
            ToolNames.GetBindings,
            ToolNames.GetSourceMapping,
            ToolNames.DetectLayoutIssues,
            ToolNames.CheckAccessibility,
            ToolNames.StartEventTrace,
            ToolNames.ReadEventTrace,
            ToolNames.StopEventTrace
        };

        foreach (var name in runtimeToolNames) {
            var tool = ToolDefinitionCatalog.All.Single(definition => definition.Name == name);
            var properties = tool.InputSchema.GetProperty("properties");
            var required = tool.InputSchema.GetProperty("required");

            Assert.Multiple(() => {
                Assert.That(properties.TryGetProperty("bridgeInstanceId", out var instance), Is.True, name);
                Assert.That(instance.GetProperty("type").GetString(), Is.EqualTo("string"), name);
                Assert.That(
                    required.EnumerateArray().Select(value => value.GetString()),
                    Does.Not.Contain("bridgeInstanceId"),
                    name);
            });
        }
    }

    [Test]
    public void RuntimeToolSupport_RejectsMalformedBridgeInstanceId() {
        Assert.That(
            () => RuntimeToolSupport.GetBridgeInstanceId(JsonDocument.Parse("{\"bridgeInstanceId\":true}").RootElement),
            Throws.TypeOf<ToolExecutionException>().With.Property("Code").EqualTo("invalid_argument"));
        Assert.That(
            () => RuntimeToolSupport.GetBridgeInstanceId(JsonDocument.Parse("{\"bridgeInstanceId\":\"\"}").RootElement),
            Is.Null);
        Assert.That(
            () => RuntimeToolSupport.GetBridgeInstanceId(JsonDocument.Parse("{\"bridgeInstanceId\":\"" + new string('x', 129) + "\"}").RootElement),
            Throws.TypeOf<ToolExecutionException>().With.Property("Code").EqualTo("invalid_argument"));
    }

    [Test]
    [Timeout(30000)]
    public async Task Refresh_ReusesUnchangedFiles_InvalidatesChangedFiles_AndRemovesDeletedFiles() {
        var root = CreateRoot();
        var first = Path.Combine(root, "First.cs");
        var second = Path.Combine(root, "Second.cs");
        WriteSource(first, "FirstType");
        WriteSource(second, "SecondType");

        try {
            var index = new SourceIndex();
            var initial = await index.RefreshAsync(root, 10, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(initial.Metadata.Scanned, Is.EqualTo(2));
                Assert.That(initial.Metadata.Parsed, Is.EqualTo(2));
                Assert.That(initial.Metadata.Reused, Is.Zero);
                Assert.That(initial.Metadata.Removed, Is.Zero);
                Assert.That(initial.Metadata.Truncated, Is.False);
                Assert.That(initial.Types, Has.Count.EqualTo(2));
            });

            var reused = await index.RefreshAsync(root, 10, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(reused.Metadata.Parsed, Is.Zero);
                Assert.That(reused.Metadata.Reused, Is.EqualTo(2));
                Assert.That(reused.Types, Has.Count.EqualTo(2));
            });

            WriteSource(first, "FirstTypeChanged", extraLine: true);
            File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddSeconds(2));
            var changed = await index.RefreshAsync(root, 10, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(changed.Metadata.Parsed, Is.EqualTo(1));
                Assert.That(changed.Metadata.Reused, Is.EqualTo(1));
                Assert.That(changed.Types.Any(type => type.Name == "FirstTypeChanged"), Is.True);
            });

            File.Delete(second);
            var removed = await index.RefreshAsync(root, 10, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(removed.Metadata.Removed, Is.EqualTo(1));
                Assert.That(removed.Types, Has.Count.EqualTo(1));
                Assert.That(removed.Types.Single().Name, Is.EqualTo("FirstTypeChanged"));
            });
        }
        finally {
            DeleteRoot(root);
        }
    }

    [Test]
    [Timeout(30000)]
    public async Task Refresh_ReportsBoundedTruncation_AndDoesNotRemoveUnseenCachedFiles() {
        var root = CreateRoot();
        try {
            for (var index = 0; index < 3; index++)
                WriteSource(Path.Combine(root, $"File{index}.cs"), $"Type{index}");

            var sourceIndex = new SourceIndex();
            var truncated = await sourceIndex.RefreshAsync(root, 2, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(truncated.Metadata.Scanned, Is.EqualTo(2));
                Assert.That(truncated.Metadata.MaxFiles, Is.EqualTo(2));
                Assert.That(truncated.Metadata.Truncated, Is.True);
                Assert.That(truncated.Types, Has.Count.EqualTo(2));
            });

            var complete = await sourceIndex.RefreshAsync(root, 3, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(complete.Metadata.Scanned, Is.EqualTo(3));
                Assert.That(complete.Metadata.Truncated, Is.False);
                Assert.That(complete.Metadata.Reused, Is.EqualTo(2));
                Assert.That(complete.Metadata.Parsed, Is.EqualTo(1));
                Assert.That(complete.Metadata.Removed, Is.Zero);
                Assert.That(complete.Types, Has.Count.EqualTo(3));
            });
        }
        finally {
            DeleteRoot(root);
        }
    }

    [Test]
    [Timeout(30000)]
    public async Task Refresh_CancellationDoesNotCommitPartialIndex() {
        var root = CreateRoot();
        try {
            for (var index = 0; index < 8; index++)
                WriteSource(Path.Combine(root, $"File{index}.cs"), $"Type{index}", extraLine: true);

            var sourceIndex = new SourceIndex();
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.That(
                async () => await sourceIndex.RefreshAsync(root, 100, cancelled.Token),
                Throws.InstanceOf<OperationCanceledException>());

            var afterCancellation = await sourceIndex.RefreshAsync(root, 100, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(afterCancellation.Metadata.Parsed, Is.EqualTo(8));
                Assert.That(afterCancellation.Metadata.Reused, Is.Zero);
                Assert.That(afterCancellation.Types, Has.Count.EqualTo(8));
            });
        }
        finally {
            DeleteRoot(root);
        }
    }

    [Test]
    [Timeout(30000)]
    public async Task Refresh_SerializesConcurrentCallsForOneRoot() {
        var root = CreateRoot();
        try {
            WriteSource(Path.Combine(root, "Concurrent.cs"), "ConcurrentType", extraLine: true);
            var sourceIndex = new SourceIndex();
            var results = await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(_ => sourceIndex.RefreshAsync(root, 10, CancellationToken.None)));

            Assert.Multiple(() => {
                Assert.That(results, Has.Length.EqualTo(8));
                Assert.That(results.All(result => result.Types.Count == 1), Is.True);
                Assert.That(results.Sum(result => result.Metadata.Parsed), Is.EqualTo(1));
                Assert.That(results.Sum(result => result.Metadata.Reused), Is.EqualTo(7));
                Assert.That(results.Any(result => result.Metadata.ParseErrors != 0), Is.False);
            });
        }
        finally {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot() {
        var root = Path.Combine(Path.GetTempPath(), $"winforms-mcp-index-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteSource(string path, string typeName, bool extraLine = false) {
        var source = $"namespace Demo;\ninternal partial class {typeName} {{\n    private int Value;\n}}\n";
        if (extraLine)
            source += "// fingerprint change\n";
        File.WriteAllText(path, source);
    }

    private static void DeleteRoot(string root) {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}