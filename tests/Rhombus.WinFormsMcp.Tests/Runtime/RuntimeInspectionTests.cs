using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Rhombus.WinFormsMcp.RuntimeContracts;
using Rhombus.WinFormsMcp.Server;
using Rhombus.WinFormsMcp.Server.Automation;
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
            var diagnostics = await client.DetectDiagnosticsAsync(
                process.Id,
                null,
                ["layout", "dpi", "bindings"],
                maxDepth: 5,
                maxNodes: 200,
                maxDiagnostics: 100,
                CancellationToken.None);
            var accessibility = await client.GetAccessibilityAsync(
                process.Id,
                null,
                maxDepth: 5,
                maxNodes: 100,
                maxDiagnostics: 100,
                CancellationToken.None);

            Assert.Multiple(() => {
                Assert.That(tree.Truncated, Is.False);
                Assert.That(inspection.Summary.Identity.Type, Does.EndWith(".Button"));
                Assert.That(inspection.Summary.Identity.OwnerType, Does.EndWith(".Form1"));
                Assert.That(status?.Process?.BridgeVersion, Is.EqualTo("1.5.12-beta"));
                Assert.That(status?.Capabilities, Does.Contain("providerSemantics"));
                Assert.That(status?.Capabilities, Does.Contain("diagnostics"));
                Assert.That(status?.Capabilities, Does.Contain("eventTrace"));
                Assert.That(inspection.Properties.Values["Name"].GetString(), Is.EqualTo("clickButton"));
                Assert.That(inspection.Properties.Values, Does.ContainKey("AccessibleName"));
                Assert.That(inspection.Layout.Bounds.Width, Is.EqualTo(100));
                Assert.That(inspection.Provider, Is.Null);
                Assert.That(inspection.Semantic, Is.Null);
                Assert.That(ancestors.Select(item => item.Name), Does.Contain("mainPanel"));
                Assert.That(ancestors.Select(item => item.Name), Does.Contain("TestForm"));
                Assert.That(bindings, Has.Some.Matches<ControlBindingSnapshot>(binding =>
                    binding.Property == "Text" &&
                    binding.DataMember == "DeviceName" &&
                    binding.DataSourceType!.Contains("BindingModel", StringComparison.Ordinal) &&
                    binding.FormattingEnabled &&
                    binding.DataSourceUpdateMode == "OnPropertyChanged" &&
                    binding.DataMemberExists == true &&
                    binding.ControlPropertyExists == true));
                Assert.That(shallowTree.Truncated, Is.True);
                Assert.That(windows.SelectMany(FlattenWindows).Select(window => window.Hwnd), Is.Unique);
                Assert.That(diagnostics.ScannedNodes, Is.GreaterThan(0));
                Assert.That(diagnostics.Checks, Is.EquivalentTo(new[] { "bindings", "dpi", "layout" }));
                Assert.That(diagnostics.Diagnostics.All(item => item.Evidence.Count > 0), Is.True);
                Assert.That(accessibility.ScannedNodes, Is.GreaterThan(0));
                Assert.That(accessibility.Controls.Select(item => item.Summary.Identity.Name), Does.Contain("textBox"));
            });

            var semanticInspection = await client.InspectControlAsync(
                process.Id,
                button.Summary.Identity.ManagedId,
                ["provider", "semantic"],
                null,
                CancellationToken.None);
            var semantic = semanticInspection.Semantic;

            Assert.Multiple(() => {
                Assert.That(semanticInspection.Provider?.ProviderName, Is.EqualTo("StandardWinForms"));
                Assert.That(semanticInspection.Provider?.SemanticType, Is.EqualTo("button"));
                Assert.That(semantic, Is.Not.Null);
                Assert.That(semantic!.ProviderName, Is.EqualTo("StandardWinForms"));
                Assert.That(semantic.SemanticType, Is.EqualTo("button"));
                Assert.That(semantic.SupportedInteractionHints, Does.Contain("invoke"));
                Assert.That(semantic.State["enabled"].GetBoolean(), Is.True);
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
    [Timeout(30000)]
    public async Task TestAppRuntimeBridge_EventTraceCapturesBoundedTextChangedEvent() {
        var executable = Path.Combine(TestContext.CurrentContext.TestDirectory, "Rhombus.WinFormsMcp.TestApp.exe");
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

            var tree = await client.GetControlTreeAsync(process.Id, null, 5, 200, CancellationToken.None);
            var textBox = Flatten(tree.Roots).Single(node => node.Summary.Identity.Name == "textBox");
            var trace = await client.StartEventTraceAsync(
                process.Id,
                textBox.Summary.Identity.ManagedId,
                ["TextChanged"],
                maxEvents: 8,
                durationMs: 10_000,
                maxNodes: 1,
                CancellationToken.None);

            var hwnd = ParseHwnd(textBox.Summary.Identity.Hwnd);
            Assert.That(hwnd, Is.Not.EqualTo(IntPtr.Zero));
            var sent = SendMessageTimeout(
                hwnd,
                0x000C,
                IntPtr.Zero,
                "Runtime trace value",
                0x0002,
                1_000,
                out _);
            Assert.That(sent, Is.Not.EqualTo(IntPtr.Zero));

            RuntimeEventTraceSnapshot? read = null;
            var eventDeadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < eventDeadline) {
                read = await client.ReadEventTraceAsync(process.Id, trace.TraceId, 0, 8, CancellationToken.None);
                if (read.Events.Count > 0)
                    break;
                await Task.Delay(50);
            }
            var stopped = await client.StopEventTraceAsync(process.Id, trace.TraceId, CancellationToken.None);

            Assert.Multiple(() => {
                Assert.That(trace.Active, Is.True);
                Assert.That(trace.SubscribedControlCount, Is.EqualTo(1));
                Assert.That(read?.Events, Has.Count.GreaterThanOrEqualTo(1));
                Assert.That(read?.Events[0].EventName, Is.EqualTo("TextChanged"));
                Assert.That(read?.Events[0].ControlId, Is.EqualTo(textBox.Summary.Identity.ManagedId));
                Assert.That(read?.Events[0].Evidence, Does.ContainKey("state"));
                Assert.That(stopped.Active, Is.False);
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
    [Timeout(45000)]
    public async Task AntdUITestAppRuntimeBridge_ReturnsProviderSemanticsAndSupportsUiaAction() {
        var executable = Path.Combine(TestContext.CurrentContext.TestDirectory, "Rhombus.WinFormsMcp.AntdUI.TestApp.exe");
        Assert.That(File.Exists(executable), Is.True, $"AntdUI test app was not found at {executable}");

        using var process = Process.Start(new ProcessStartInfo {
            FileName = executable,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("AntdUI test app could not be started.");
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
            Assert.That(status?.Capabilities, Does.Contain("providerSemantics"));

            var tree = await client.GetControlTreeAsync(
                process.Id,
                null,
                maxDepth: 6,
                maxNodes: 200,
                CancellationToken.None);
            var input = Flatten(tree.Roots)
                .Single(node => node.Summary.Identity.Name == "antdInput");
            var inspection = await client.InspectControlAsync(
                process.Id,
                input.Summary.Identity.ManagedId,
                ["identity", "state", "layout", "provider", "semantic"],
                null,
                CancellationToken.None);
            var select = Flatten(tree.Roots)
                .Single(node => node.Summary.Identity.Name == "antdSelect");
            var selectInspection = await client.InspectControlAsync(
                process.Id,
                select.Summary.Identity.ManagedId,
                ["provider", "semantic"],
                null,
                CancellationToken.None);
            var tabs = Flatten(tree.Roots)
                .Single(node => node.Summary.Identity.Name == "antdTabs");
            var tabsInspection = await client.InspectControlAsync(
                process.Id,
                tabs.Summary.Identity.ManagedId,
                ["provider", "semantic"],
                null,
                CancellationToken.None);
            var tabsPagedInspection = await client.InspectControlAsync(
                process.Id,
                tabs.Summary.Identity.ManagedId,
                ["provider", "semantic"],
                null,
                CancellationToken.None,
                new ControlSemanticOptions {
                    Start = 1,
                    Count = 1,
                    MaxNodes = 20
                });
            var antTree = Flatten(tree.Roots)
                .Single(node => node.Summary.Identity.Name == "antdTree");
            var treeInspection = await client.InspectControlAsync(
                process.Id,
                antTree.Summary.Identity.ManagedId,
                ["provider", "semantic"],
                null,
                CancellationToken.None);
            var table = Flatten(tree.Roots)
                .Single(node => node.Summary.Identity.Name == "antdTable");
            var tableInspection = await client.InspectControlAsync(
                process.Id,
                table.Summary.Identity.ManagedId,
                ["provider", "semantic"],
                null,
                CancellationToken.None);
            var tablePagedInspection = await client.InspectControlAsync(
                process.Id,
                table.Summary.Identity.ManagedId,
                ["provider", "semantic"],
                null,
                CancellationToken.None,
                new ControlSemanticOptions {
                    StartRow = 1,
                    RowCount = 1,
                    RowScope = "data",
                    MaxNodes = 80
                });
            var tableRenderedInspection = await client.InspectControlAsync(
                process.Id,
                table.Summary.Identity.ManagedId,
                ["provider", "semantic"],
                null,
                CancellationToken.None,
                new ControlSemanticOptions {
                    RowCount = 2,
                    RowScope = "rendered",
                    MaxNodes = 100
                });
            var menu = Flatten(tree.Roots)
                .Single(node => node.Summary.Identity.Name == "antdMenu");
            var menuInspection = await client.InspectControlAsync(
                process.Id,
                menu.Summary.Identity.ManagedId,
                ["provider", "semantic"],
                null,
                CancellationToken.None);

            Assert.Multiple(() => {
                Assert.That(tree.Truncated, Is.False);
                Assert.That(input.Summary.Identity.Type, Is.EqualTo("AntdUI.Input"));
                Assert.That(inspection.Provider?.ProviderName, Is.EqualTo("AntdUI"));
                Assert.That(inspection.Provider?.SemanticType, Is.EqualTo("textbox"));
                Assert.That(inspection.Provider?.RuntimeType, Is.EqualTo("AntdUI.Input"));
                Assert.That(inspection.Provider?.ProviderVersion, Is.Not.Empty);
                Assert.That(inspection.State.Text, Is.EqualTo("Initial input"));
                Assert.That(inspection.Layout.Bounds.Width, Is.GreaterThan(0));
                Assert.That(inspection.Semantic?.SemanticType, Is.EqualTo("textbox"));
                Assert.That(inspection.Semantic?.State["text"].GetString(), Is.EqualTo("Initial input"));
                Assert.That(inspection.Semantic?.Properties["placeholderText"].GetString(), Is.EqualTo("Search devices"));
                Assert.That(inspection.Semantic?.Properties["prefixText"].GetString(), Is.EqualTo("SN"));
                Assert.That(inspection.Semantic?.Properties["suffixText"].GetString(), Is.EqualTo("OK"));
                Assert.That(inspection.Semantic?.Properties["status"].GetString(), Does.Contain("Success"));
                Assert.That(selectInspection.Provider?.SemanticType, Is.EqualTo("select"));
                Assert.That(selectInspection.Semantic?.State["selectedIndex"].GetInt32(), Is.EqualTo(1));
                Assert.That(selectInspection.Semantic?.State["selectedValue"].GetString(), Is.EqualTo("B"));
                Assert.That(selectInspection.Semantic?.Children, Has.Count.EqualTo(2));
                Assert.That(tabsInspection.Provider?.SemanticType, Is.EqualTo("tabs"));
                Assert.That(tabsInspection.Semantic?.Children[1].Text, Is.EqualTo("Devices"));
                Assert.That(tabsInspection.Semantic?.Children[1].State["selected"].GetBoolean(), Is.True);
                Assert.That(tabsPagedInspection.Semantic?.Children, Has.Count.EqualTo(1));
                Assert.That(tabsPagedInspection.Semantic?.Children[0].Index, Is.EqualTo(1));
                Assert.That(tabsPagedInspection.Semantic?.Truncated, Is.True);
                Assert.That(treeInspection.Provider?.SemanticType, Is.EqualTo("tree"));
                Assert.That(treeInspection.Semantic?.Children[0].Text, Is.EqualTo("Devices"));
                Assert.That(treeInspection.Semantic?.Children[0].Children[0].Text, Is.EqualTo("Router"));
                Assert.That(treeInspection.Semantic?.Children[0].Children[0].State["selected"].GetBoolean(), Is.True);
                Assert.That(tableInspection.Provider?.SemanticType, Is.EqualTo("table"));
                Assert.That(tableInspection.Semantic?.Children.Single(node => node.Kind == "columns").Children, Has.Count.EqualTo(4));
                Assert.That(tableInspection.Semantic?.Children.Single(node => node.Kind == "rows").Children[0].Children.Single(cell => cell.Name == "DeviceName").Text, Is.EqualTo("Router"));
                var actionButton = tableInspection.Semantic?.Children
                    .Single(node => node.Kind == "rows").Children[0].Children
                    .Single(cell => cell.Name == "Actions").Children.Single(node => node.Kind == "cell-button");
                Assert.That(actionButton?.Name, Is.EqualTo("open"));
                Assert.That(tablePagedInspection.Semantic?.Metadata["rowScope"].GetString(), Is.EqualTo("data"));
                Assert.That(tablePagedInspection.Semantic?.Metadata["startRow"].GetInt32(), Is.EqualTo(1));
                Assert.That(tablePagedInspection.Semantic?.Children.Single(node => node.Kind == "rows").Children, Has.Count.EqualTo(1));
                Assert.That(tablePagedInspection.Semantic?.Children.Single(node => node.Kind == "rows").Children[0].Children.Single(cell => cell.Name == "DeviceName").Text, Is.EqualTo("Switch"));
                Assert.That(tableRenderedInspection.Semantic?.Metadata["requestedRowScope"].GetString(), Is.EqualTo("rendered"));
                Assert.That(tableRenderedInspection.Semantic?.Metadata, Does.ContainKey("effectiveRowScope"));
                Assert.That(menuInspection.Provider?.SemanticType, Is.EqualTo("menu"));
                Assert.That(menuInspection.Semantic?.Children[0].Text, Is.EqualTo("File"));
                Assert.That(menuInspection.Semantic?.Children[0].Children[1].State["enabled"].GetBoolean(), Is.False);
            });

            using var automation = new AutomationHelper(logger: NullLogger<AutomationHelper>.Instance);
            using var session = new SessionManager(automation);
            var mainWindow = automation.GetMainWindow(process.Id);
            Assert.That(mainWindow, Is.Not.Null);

            var correlation = new ManagedUiaCorrelationService(session).TryCorrelate(inspection.Summary.Identity);
            Assert.Multiple(() => {
                Assert.That(correlation, Is.Not.Null);
                Assert.That(correlation!.UiaId, Is.Not.Empty);
                Assert.That(
                    correlation.Method,
                    Is.AnyOf("automationId", "nativeWindowHandle", "nativeWindowHandleTraversal"));
                Assert.That(correlation.Confidence, Is.GreaterThanOrEqualTo(0.85d));
            });

            var uiaElement = session.GetElement(correlation!.UiaId!);
            Assert.That(uiaElement, Is.Not.Null);
            automation.Click(uiaElement!);
            automation.TypeText(uiaElement!, "Typed via UIA", clearFirst: true);

            var typedInspection = await WaitForRuntimeTextAsync(
                client,
                process.Id,
                input.Summary.Identity.ManagedId,
                "Typed via UIA");

            Assert.That(typedInspection.State.Text, Is.EqualTo("Typed via UIA"));
        }
        finally {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Test]
    [Timeout(45000)]
    public async Task AntdUISelectDropdown_WindowTreeReturnsLayeredMetadataAndBoundedItems() {
        var executable = Path.Combine(TestContext.CurrentContext.TestDirectory, "Rhombus.WinFormsMcp.AntdUI.TestApp.exe");
        Assert.That(File.Exists(executable), Is.True, $"AntdUI test app was not found at {executable}");

        var startInfo = new ProcessStartInfo {
            FileName = executable,
            UseShellExecute = false
        };
        startInfo.EnvironmentVariables["WINFORMS_MCP_OPEN_ANTDUI_POPUP"] = "1";
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("AntdUI test app could not be started.");
        using var client = new NamedPipeRuntimeBridgeClient(
            Options.Create(new McpServerOptions {
                RuntimeBridgeConnectTimeoutMs = 250,
                RuntimeBridgeRequestTimeoutMs = 3000
            }),
            NullLogger<NamedPipeRuntimeBridgeClient>.Instance);

        try {
            BridgeStatus? status = null;
            var statusDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < statusDeadline) {
                status = await client.GetStatusAsync(process.Id, CancellationToken.None);
                if (status.Available)
                    break;
                await Task.Delay(100);
            }

            Assert.That(status?.Available, Is.True, status?.Error);
            var tree = await client.GetControlTreeAsync(
                process.Id,
                null,
                maxDepth: 6,
                maxNodes: 400,
                CancellationToken.None);
            var select = Flatten(tree.Roots)
                .Single(node => node.Summary.Identity.Name == "antdSelect");

            IReadOnlyList<WindowSnapshot> windows = [];
            WindowSnapshot? dropdown = null;
            var windowDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < windowDeadline) {
                windows = await client.GetWindowTreeAsync(
                    process.Id,
                    maxNodes: 400,
                    CancellationToken.None,
                    maxItems: 10);
                dropdown = windows
                    .SelectMany(FlattenWindows)
                    .FirstOrDefault(window =>
                        window.ProviderWindowMetadata?.SemanticType == "select-dropdown");
                if (dropdown is not null)
                    break;
                await Task.Delay(100);
            }

            var metadata = dropdown?.ProviderWindowMetadata;
            Assert.Multiple(() => {
                Assert.That(dropdown, Is.Not.Null);
                Assert.That(dropdown?.Kind, Is.EqualTo("Popup"));
                Assert.That(metadata?.Provider, Is.EqualTo("AntdUI"));
                Assert.That(metadata?.RuntimeWindowType, Does.Contain("LayeredFormSelectDown"));
                Assert.That(metadata?.OwnerControlId, Is.EqualTo(select.Summary.Identity.ManagedId));
                Assert.That(metadata?.OwnerControlPath, Does.EndWith("/antdSelect"));
                Assert.That(metadata?.Hwnd, Is.EqualTo(dropdown?.Hwnd));
                Assert.That(metadata?.Dpi, Is.GreaterThan(0));
                Assert.That(metadata?.Items.Select(item => item.Text), Does.Contain("Alpha"));
                Assert.That(metadata?.Items.Select(item => item.Text), Does.Contain("Beta"));
                Assert.That(metadata?.SelectedItem?.Text, Is.EqualTo("Beta"));
                Assert.That(metadata?.VisibleRange, Is.Not.Null);
            });

            var limitedWindows = await client.GetWindowTreeAsync(
                process.Id,
                maxNodes: 400,
                CancellationToken.None,
                maxItems: 1);
            var limitedMetadata = limitedWindows
                .SelectMany(FlattenWindows)
                .First(window => window.ProviderWindowMetadata?.SemanticType == "select-dropdown")
                .ProviderWindowMetadata!;
            Assert.Multiple(() => {
                Assert.That(limitedMetadata.Items, Has.Count.EqualTo(1));
                Assert.That(limitedMetadata.Truncated, Is.True);
            });
        }
        finally {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [TestCase("menu", "menu-popup", "antdMenu")]
    [TestCase("tooltip", "tooltip", "antdSelect")]
    [TestCase("message", "message", "AntdUiInspectionForm")]
    [TestCase("drawer", "drawer", "AntdUiInspectionForm")]
    [Timeout(45000)]
    public async Task AntdUILayeredSurface_WindowTreeCorrelatesOwner(
        string fixture,
        string semanticType,
        string ownerName) {
        var executable = Path.Combine(TestContext.CurrentContext.TestDirectory, "Rhombus.WinFormsMcp.AntdUI.TestApp.exe");
        Assert.That(File.Exists(executable), Is.True, $"AntdUI test app was not found at {executable}");

        var startInfo = new ProcessStartInfo {
            FileName = executable,
            UseShellExecute = false
        };
        startInfo.EnvironmentVariables["WINFORMS_MCP_OPEN_ANTDUI_POPUP"] = fixture;
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("AntdUI test app could not be started.");
        using var client = new NamedPipeRuntimeBridgeClient(
            Options.Create(new McpServerOptions {
                RuntimeBridgeConnectTimeoutMs = 250,
                RuntimeBridgeRequestTimeoutMs = 3000
            }),
            NullLogger<NamedPipeRuntimeBridgeClient>.Instance);

        try {
            BridgeStatus? status = null;
            var statusDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < statusDeadline) {
                status = await client.GetStatusAsync(process.Id, CancellationToken.None);
                if (status.Available)
                    break;
                await Task.Delay(100);
            }

            Assert.That(status?.Available, Is.True, status?.Error);
            var tree = await client.GetControlTreeAsync(
                process.Id,
                null,
                maxDepth: 6,
                maxNodes: 400,
                CancellationToken.None);
            var owner = Flatten(tree.Roots)
                .Single(node => node.Summary.Identity.Name == ownerName);

            WindowSnapshot? surface = null;
            var windowDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < windowDeadline) {
                var windows = await client.GetWindowTreeAsync(
                    process.Id,
                    maxNodes: 400,
                    CancellationToken.None,
                    maxItems: 20);
                surface = windows
                    .SelectMany(FlattenWindows)
                    .FirstOrDefault(window => window.ProviderWindowMetadata?.SemanticType == semanticType);
                if (surface is not null)
                    break;
                await Task.Delay(100);
            }

            var metadata = surface?.ProviderWindowMetadata;
            Assert.Multiple(() => {
                Assert.That(surface, Is.Not.Null, $"The {fixture} fixture did not expose a managed layered window.");
                Assert.That(metadata?.Provider, Is.EqualTo("AntdUI"));
                Assert.That(metadata?.SemanticType, Is.EqualTo(semanticType));
                Assert.That(metadata?.OwnerControlId, Is.EqualTo(owner.Summary.Identity.ManagedId));
                Assert.That(metadata?.OwnerControlName, Is.EqualTo(ownerName));
                Assert.That(metadata?.Hwnd, Is.EqualTo(surface?.Hwnd));
                Assert.That(metadata?.Bounds.Width, Is.GreaterThan(0));
                Assert.That(metadata?.Visible, Is.True);
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

    private static async Task<ControlInspectionSnapshot> WaitForRuntimeTextAsync(
        NamedPipeRuntimeBridgeClient client,
        int processId,
        string controlId,
        string expectedText) {
        ControlInspectionSnapshot? last = null;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) {
            last = await client.InspectControlAsync(
                processId,
                controlId,
                ["state", "semantic"],
                null,
                CancellationToken.None);
            if (string.Equals(last.State.Text, expectedText, StringComparison.Ordinal))
                return last;
            await Task.Delay(100);
        }

        return last ?? throw new InvalidOperationException("RuntimeBridge did not return a control inspection.");
    }

    private static IntPtr ParseHwnd(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return IntPtr.Zero;
        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return long.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var handle)
            ? new IntPtr(handle)
            : IntPtr.Zero;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        string lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}