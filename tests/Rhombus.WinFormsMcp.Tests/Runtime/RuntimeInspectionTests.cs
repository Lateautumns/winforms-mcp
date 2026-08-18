using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Rhombus.WinFormsMcp.RuntimeContracts;
using Rhombus.WinFormsMcp.Server;
using Rhombus.WinFormsMcp.Server.Runtime;

namespace Rhombus.WinFormsMcp.Tests.Runtime;

[TestFixture]
public sealed class RuntimeInspectionTests {
    [Test]
    [Timeout(30000)]
    public async Task TestAppRuntimeBridge_ReturnsManagedTreeInspectionAndAncestors() {
        var executable = Path.Combine(TestContext.CurrentContext.TestDirectory, "Rhombus.WinFormsMcp.TestApp.exe");
        Assert.That(File.Exists(executable), Is.True, $"Test app was not found at {executable}");

        using var process = Process.Start(new ProcessStartInfo {
            FileName = executable,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Test app could not be started.");
        using var client = new NamedPipeRuntimeBridgeClient(
            Options.Create(new McpServerOptions {
                RuntimeBridgeConnectTimeoutMs = 250,
                RuntimeBridgeRequestTimeoutMs = 3000
            }),
            NullLogger<NamedPipeRuntimeBridgeClient>.Instance);

        try {
            BridgeStatus? status = null;
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline) {
                status = await client.GetStatusAsync(process.Id, CancellationToken.None);
                if (status.Available)
                    break;
                await Task.Delay(100);
            }

            Assert.That(status?.Available, Is.True, status?.Error);
            var tree = await client.GetControlTreeAsync(
                process.Id,
                null,
                maxDepth: 5,
                maxNodes: 200,
                CancellationToken.None);
            var button = Flatten(tree.Roots)
                .Single(node => node.Summary.Identity.Name == "clickButton");
            var inspection = await client.InspectControlAsync(
                process.Id,
                button.Summary.Identity.ManagedId,
                ["identity", "state", "properties", "layout", "bindings"],
                ["AccessibleName"],
                CancellationToken.None);
            var ancestors = await client.GetAncestorsAsync(
                process.Id,
                button.Summary.Identity.ManagedId,
                CancellationToken.None);
            var bindings = await client.GetBindingsAsync(
                process.Id,
                Flatten(tree.Roots)
                    .Single(node => node.Summary.Identity.Name == "textBox")
                    .Summary.Identity.ManagedId,
                CancellationToken.None);
            var shallowTree = await client.GetControlTreeAsync(
                process.Id,
                null,
                maxDepth: 1,
                maxNodes: 200,
                CancellationToken.None);
            var windows = await client.GetWindowTreeAsync(
                process.Id,
                maxNodes: 200,
                CancellationToken.None);

            Assert.Multiple(() => {
                Assert.That(tree.Truncated, Is.False);
                Assert.That(inspection.Summary.Identity.Type, Does.EndWith(".Button"));
                Assert.That(inspection.Summary.Identity.OwnerType, Does.EndWith(".Form1"));
                Assert.That(status?.Process?.BridgeVersion, Is.EqualTo("1.5.12-beta"));
                Assert.That(inspection.Properties.Values["Name"].GetString(), Is.EqualTo("clickButton"));
                Assert.That(inspection.Properties.Values, Does.ContainKey("AccessibleName"));
                Assert.That(inspection.Layout.Bounds.Width, Is.EqualTo(100));
                Assert.That(ancestors.Select(item => item.Name), Does.Contain("mainPanel"));
                Assert.That(ancestors.Select(item => item.Name), Does.Contain("TestForm"));
                Assert.That(bindings, Has.Some.Matches<ControlBindingSnapshot>(binding =>
                    binding.Property == "Text" &&
                    binding.DataMember == "DeviceName" &&
                    binding.DataSourceType!.Contains("BindingModel", StringComparison.Ordinal) &&
                    binding.FormattingEnabled &&
                    binding.DataSourceUpdateMode == "OnPropertyChanged"));
                Assert.That(shallowTree.Truncated, Is.True);
                Assert.That(windows.SelectMany(FlattenWindows).Select(window => window.Hwnd), Is.Unique);
            });
        }
        finally {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Test]
    public async Task SourceMappingService_MapsDesignerInitializationAndEventSymbol() {
        var root = Path.Combine(Path.GetTempPath(), $"winforms-mcp-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var designer = Path.Combine(root, "TestForm.Designer.cs");
        var codeBehind = Path.Combine(root, "TestForm.cs");
        File.WriteAllText(designer, """
            namespace Demo;
            partial class TestForm {
                private System.Windows.Forms.Button btnSave;
                private void InitializeComponent() {
                    this.btnSave = new System.Windows.Forms.Button();
                    this.btnSave.Click += new System.EventHandler(this.BtnSave_Click);
                }
            }
            """);
        File.WriteAllText(codeBehind, """
            namespace Demo;
            partial class TestForm {
                private void BtnSave_Click(object sender, System.EventArgs e) { }
            }
            """);
        File.WriteAllText(Path.Combine(root, "000-Other.TestForm.cs"), """
            namespace Other;
            partial class TestForm { }
            """);

        try {
            var service = new SourceMappingService();
            var mapping = await service.MapAsync(
                Environment.ProcessId,
                new ControlIdentity {
                    ManagedId = "ctrl_1",
                    Name = "btnSave",
                    Type = "System.Windows.Forms.Button",
                    OwnerType = "Demo.TestForm",
                    ProcessId = Environment.ProcessId,
                    ControlPath = "TestForm/btnSave"
                },
                root,
                CancellationToken.None);

            Assert.Multiple(() => {
                Assert.That(mapping.FullyQualifiedType, Is.EqualTo("Demo.TestForm"));
                Assert.That(mapping.Declaration?.File, Is.EqualTo(designer));
                Assert.That(mapping.Initialization?.Line, Is.EqualTo(4));
                Assert.That(mapping.Events["Click"].File, Is.EqualTo(codeBehind));
                Assert.That(mapping.Events["Click"].FullyQualifiedSymbol, Is.EqualTo("Demo.TestForm.BtnSave_Click"));
                Assert.That(mapping.Warnings, Is.Empty);
            });
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task RuntimeBridgeClient_StatusIsUnavailableWithoutTargetBridge() {
        var options = Options.Create(new McpServerOptions {
            RuntimeBridgeEnabled = true,
            RuntimeBridgeConnectTimeoutMs = 50,
            RuntimeBridgeRequestTimeoutMs = 100
        });
        using var client = new NamedPipeRuntimeBridgeClient(
            options,
            NullLogger<NamedPipeRuntimeBridgeClient>.Instance);

        var status = await client.GetStatusAsync(Environment.ProcessId, CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(status.Available, Is.False);
            Assert.That(status.Connected, Is.False);
            Assert.That(status.PipeName, Is.EqualTo(RuntimeBridgeProtocol.GetPipeName(Environment.ProcessId)));
            Assert.That(status.Error, Is.Not.Empty);
        });
    }

    private static IEnumerable<ControlTreeNode> Flatten(IEnumerable<ControlTreeNode> nodes) {
        foreach (var node in nodes) {
            yield return node;
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }

    private static IEnumerable<WindowSnapshot> FlattenWindows(WindowSnapshot window) {
        yield return window;
        foreach (var child in window.Children.SelectMany(FlattenWindows))
            yield return child;
    }
}
