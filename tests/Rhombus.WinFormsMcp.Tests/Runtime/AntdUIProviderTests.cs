using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

namespace Rhombus.WinFormsMcp.Tests.Runtime;

[TestFixture]
public sealed class AntdUIProviderTests {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Registry_PrefersAntdUIProviderOverStandardFallback() {
        using var button = new global::AntdUI.Button {
            Text = "Save"
        };
        var registry = ControlProviderRegistry.CreateDefault();

        var provider = registry.Resolve(button);

        Assert.That(provider.ProviderName, Is.EqualTo("AntdUI"));
        Assert.That(provider.Describe(button).SemanticType, Is.EqualTo("button"));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void AntdUIProvider_ReadsButtonInputAndSwitchSemantics() {
        var provider = new AntdUIProvider();
        using var button = new global::AntdUI.Button {
            Text = "Run",
            Loading = true,
            Toggle = true,
            IconSvg = "<svg />"
        };
        TrySetProperty(button, "Type", "Primary");
        TrySetProperty(button, "Shape", "Round");
        TrySetProperty(button, "Radius", 6);

        using var input = new global::AntdUI.Input {
            Text = "Device",
            PlaceholderText = "Search",
            PrefixText = "SN",
            SuffixSvg = "<svg />",
            ReadOnly = true,
            Multiline = false
        };
        TrySetProperty(input, "Status", "Success");

        using var toggle = new global::AntdUI.Switch {
            Checked = true,
            Loading = true
        };
        var context = CreateContext();

        var buttonSemantic = provider.Inspect(button, context);
        var inputSemantic = provider.Inspect(input, context);
        var switchSemantic = provider.Inspect(toggle, context);

        Assert.Multiple(() => {
            Assert.That(buttonSemantic.SemanticType, Is.EqualTo("button"));
            Assert.That(buttonSemantic.State["loading"].GetBoolean(), Is.True);
            Assert.That(buttonSemantic.State["toggle"].GetBoolean(), Is.True);
            Assert.That(buttonSemantic.Properties["type"].GetString(), Does.Contain("Primary"));
            Assert.That(buttonSemantic.Properties["shape"].GetString(), Does.Contain("Round"));
            Assert.That(buttonSemantic.Properties["iconSvg"].GetString(), Is.EqualTo("<svg />"));
            Assert.That(inputSemantic.SemanticType, Is.EqualTo("textbox"));
            Assert.That(inputSemantic.State["readOnly"].GetBoolean(), Is.True);
            Assert.That(inputSemantic.Properties["placeholderText"].GetString(), Is.EqualTo("Search"));
            Assert.That(inputSemantic.Properties["prefixText"].GetString(), Is.EqualTo("SN"));
            Assert.That(inputSemantic.Properties["suffixSvg"].GetString(), Is.EqualTo("<svg />"));
            Assert.That(inputSemantic.Properties["status"].GetString(), Does.Contain("Success"));
            Assert.That(switchSemantic.SemanticType, Is.EqualTo("switch"));
            Assert.That(switchSemantic.State["checked"].GetBoolean(), Is.True);
            Assert.That(switchSemantic.State["loading"].GetBoolean(), Is.True);
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void AntdUIProvider_ReturnsBoundedSelectItems() {
        var provider = new AntdUIProvider();
        using var select = new global::AntdUI.Select {
            Text = "Beta"
        };
        select.Items.Add(new global::AntdUI.SelectItem("Alpha", "A") {
            SubText = "First",
            IconSvg = "<svg-a />"
        });
        select.Items.Add(new global::AntdUI.SelectItem("Beta", "B") {
            SubText = "Second"
        });
        select.SelectedValue = "B";

        var semantic = provider.Inspect(
            select,
            new ControlProviderContext(
                maxDepth: 4,
                maxNodes: 1,
                getControlId: control => control.Name,
                toJsonValue: ToJsonValue));

        Assert.Multiple(() => {
            Assert.That(semantic.SemanticType, Is.EqualTo("select"));
            Assert.That(semantic.State["selectedIndex"].GetInt32(), Is.EqualTo(1));
            Assert.That(semantic.State["selectedValue"].GetString(), Is.EqualTo("B"));
            Assert.That(semantic.ChildCount, Is.EqualTo(2));
            Assert.That(semantic.Children, Has.Count.EqualTo(1));
            Assert.That(semantic.Children[0].Text, Is.EqualTo("Alpha"));
            Assert.That(semantic.Children[0].Value, Is.EqualTo("A"));
            Assert.That(semantic.Children[0].Properties["subText"].GetString(), Is.EqualTo("First"));
            Assert.That(semantic.Truncated, Is.True);
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void AntdUIProvider_ReadsTabsTreeTableAndMenuSemantics() {
        var provider = new AntdUIProvider();
        var context = CreateContext(maxNodes: 50);

        using var tabs = new global::AntdUI.Tabs {
            Name = "tabsMain"
        };
        tabs.Pages.Add(new global::AntdUI.TabPage { Name = "tabOverview", Text = "Overview" });
        tabs.Pages.Add(new global::AntdUI.TabPage { Name = "tabDevices", Text = "Devices" });
        tabs.SelectedIndex = 1;

        using var tree = new global::AntdUI.Tree {
            Name = "treeDevices"
        };
        var root = new global::AntdUI.TreeItem("Devices") {
            Name = "devices",
            ID = "devices",
            Checked = true,
            Expand = true
        };
        root.Sub.Add(new global::AntdUI.TreeItem("Router") {
            Name = "router",
            ID = "router",
            Checked = true
        });
        tree.Items.Add(root);
        tree.SelectItem = root.Sub[0];

        using var table = new global::AntdUI.Table {
            Name = "tableDevices",
            DataSource = new List<DeviceRow> {
                new() { DeviceName = "Router", IP = "10.0.0.1", Status = "Online" },
                new() { DeviceName = "Switch", IP = "10.0.0.2", Status = "Offline" }
            },
            SelectedIndex = 1
        };
        table.Columns.Add(new global::AntdUI.Column("DeviceName", "Device Name"));
        table.Columns.Add(new global::AntdUI.Column("IP", "IP"));
        table.Columns.Add(new global::AntdUI.Column("Status", "Status"));

        using var menu = new global::AntdUI.Menu {
            Name = "menuMain"
        };
        var file = new global::AntdUI.MenuItem("File") {
            Name = "file",
            ID = "file",
            Expand = true
        };
        file.Sub.Add(new global::AntdUI.MenuItem("Open") {
            Name = "open",
            ID = "open"
        });
        file.Sub.Add(new global::AntdUI.MenuItem("Exit") {
            Name = "exit",
            ID = "exit",
            Enabled = false
        });
        menu.Items.Add(file);

        var tabsSemantic = provider.Inspect(tabs, context);
        var treeSemantic = provider.Inspect(tree, context);
        var tableSemantic = provider.Inspect(table, context);
        var menuSemantic = provider.Inspect(menu, context);

        Assert.Multiple(() => {
            Assert.That(tabsSemantic.SemanticType, Is.EqualTo("tabs"));
            Assert.That(tabsSemantic.State["selectedIndex"].GetInt32(), Is.EqualTo(1));
            Assert.That(tabsSemantic.Properties["pageCount"].GetInt32(), Is.EqualTo(2));
            Assert.That(tabsSemantic.Children[1].Kind, Is.EqualTo("tab-page"));
            Assert.That(tabsSemantic.Children[1].Text, Is.EqualTo("Devices"));
            Assert.That(tabsSemantic.Children[1].State["selected"].GetBoolean(), Is.True);

            Assert.That(treeSemantic.SemanticType, Is.EqualTo("tree"));
            Assert.That(treeSemantic.Children[0].Kind, Is.EqualTo("tree-node"));
            Assert.That(treeSemantic.Children[0].Text, Is.EqualTo("Devices"));
            Assert.That(treeSemantic.Children[0].State["checked"].GetBoolean(), Is.True);
            Assert.That(treeSemantic.Children[0].State["expanded"].GetBoolean(), Is.True);
            Assert.That(treeSemantic.Children[0].Children[0].Text, Is.EqualTo("Router"));
            Assert.That(treeSemantic.Children[0].Children[0].State["selected"].GetBoolean(), Is.True);

            Assert.That(tableSemantic.SemanticType, Is.EqualTo("table"));
            Assert.That(tableSemantic.Metadata["rowScope"].GetString(), Is.EqualTo("data"));
            var columns = tableSemantic.Children.Single(node => node.Kind == "columns");
            var rows = tableSemantic.Children.Single(node => node.Kind == "rows");
            Assert.That(columns.Children.Select(node => node.Value), Is.EqualTo(new[] { "DeviceName", "IP", "Status" }));
            Assert.That(rows.Children, Has.Count.EqualTo(2));
            Assert.That(rows.Children[0].Children.Single(cell => cell.Name == "DeviceName").Text, Is.EqualTo("Router"));
            Assert.That(rows.Children[0].Children.Single(cell => cell.Name == "Status").Text, Is.EqualTo("Online"));
            Assert.That(rows.Children[0].State["selected"].GetBoolean(), Is.True);

            Assert.That(menuSemantic.SemanticType, Is.EqualTo("menu"));
            Assert.That(menuSemantic.Children[0].Kind, Is.EqualTo("menu-item"));
            Assert.That(menuSemantic.Children[0].Text, Is.EqualTo("File"));
            Assert.That(menuSemantic.Children[0].Children[1].Text, Is.EqualTo("Exit"));
            Assert.That(menuSemantic.Children[0].Children[1].State["enabled"].GetBoolean(), Is.False);
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void AntdUIProvider_BoundsHierarchicalAndTableSemantics() {
        var provider = new AntdUIProvider();
        using var tree = new global::AntdUI.Tree();
        for (var index = 0; index < 4; index++)
            tree.Items.Add(new global::AntdUI.TreeItem("Node " + index));

        using var table = new global::AntdUI.Table {
            DataSource = Enumerable.Range(0, 10)
                .Select(index => new DeviceRow { DeviceName = "Device " + index, IP = "10.0.0." + index, Status = "Online" })
                .ToList()
        };
        table.Columns.Add(new global::AntdUI.Column("DeviceName", "Device Name"));
        table.Columns.Add(new global::AntdUI.Column("IP", "IP"));

        var boundedTree = provider.Inspect(
            tree,
            new ControlProviderContext(4, 2, control => control.Name, ToJsonValue));
        var boundedTable = provider.Inspect(
            table,
            new ControlProviderContext(4, 4, control => control.Name, ToJsonValue));

        Assert.Multiple(() => {
            Assert.That(boundedTree.Children, Has.Count.EqualTo(2));
            Assert.That(boundedTree.Truncated, Is.True);
            var rows = boundedTable.Children.Single(node => node.Kind == "rows");
            Assert.That(rows.Truncated, Is.True);
            Assert.That(rows.ChildCount, Is.EqualTo(10));
            Assert.That(boundedTable.Truncated, Is.True);
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void AntdUIProvider_PagesNavigationAndTableScopesWithoutUnboundedReads() {
        var provider = new AntdUIProvider();
        using var tabs = new global::AntdUI.Tabs();
        tabs.Pages.Add(new global::AntdUI.TabPage { Text = "Overview" });
        tabs.Pages.Add(new global::AntdUI.TabPage { Text = "Devices" });
        tabs.Pages.Add(new global::AntdUI.TabPage { Text = "Logs" });

        var pagedTabs = provider.Inspect(
            tabs,
            CreateContext(maxNodes: 10, start: 1, count: 1));

        using var tree = new global::AntdUI.Tree();
        for (var index = 0; index < 3; index++)
            tree.Items.Add(new global::AntdUI.TreeItem("Root " + index));
        var pagedTree = provider.Inspect(
            tree,
            CreateContext(maxNodes: 10, start: 1, count: 1));

        using var menu = new global::AntdUI.Menu();
        for (var index = 0; index < 3; index++)
            menu.Items.Add(new global::AntdUI.MenuItem("Item " + index));
        var pagedMenu = provider.Inspect(
            menu,
            CreateContext(maxNodes: 10, start: 1, count: 1));

        using var table = new global::AntdUI.Table {
            DataSource = new List<DeviceRow> {
                new() {
                    DeviceName = "Router",
                    IP = "10.0.0.1",
                    Status = "Online",
                    Actions = [new global::AntdUI.CellButton("open", "Open")]
                },
                new() {
                    DeviceName = "Switch",
                    IP = "10.0.0.2",
                    Status = "Offline",
                    Actions = [new global::AntdUI.CellButton("details", "Details") { Enabled = false }]
                },
                new() {
                    DeviceName = "Firewall",
                    IP = "10.0.0.3",
                    Status = "Warning",
                    Actions = [new global::AntdUI.CellButton("upgrade", "Upgrade").SetLoading()]
                }
            },
            SelectedIndex = 2
        };
        table.Columns.Add(new global::AntdUI.Column("DeviceName", "Device Name") {
            SortOrder = true,
            Filter = new global::AntdUI.FilterOption()
        });
        table.Columns.Add(new global::AntdUI.Column("IP", "IP"));
        table.Columns.Add(new global::AntdUI.Column("Status", "Status"));
        table.Columns.Add(new global::AntdUI.Column("Actions", "Actions"));
        table.SetSortIndex([2, 0, 1]);

        var dataPage = provider.Inspect(
            table,
            CreateContext(maxNodes: 80, startRow: 1, rowCount: 1, rowScope: "data"));
        var visiblePage = provider.Inspect(
            table,
            CreateContext(maxNodes: 80, rowCount: 1, rowScope: "visible"));
        var renderedFallback = provider.Inspect(
            table,
            CreateContext(maxNodes: 80, rowCount: 1, rowScope: "rendered"));

        var dataRows = dataPage.Children.Single(node => node.Kind == "rows");
        var visibleRows = visiblePage.Children.Single(node => node.Kind == "rows");
        var fallbackRows = renderedFallback.Children.Single(node => node.Kind == "rows");
        var dataAction = dataRows.Children[0].Children
            .Single(cell => cell.Name == "Actions")
            .Children.Single(button => button.Kind == "cell-button");

        Assert.Multiple(() => {
            Assert.That(pagedTabs.Children, Has.Count.EqualTo(1));
            Assert.That(pagedTabs.Children[0].Index, Is.EqualTo(1));
            Assert.That(pagedTabs.Children[0].Text, Is.EqualTo("Devices"));
            Assert.That(pagedTabs.Truncated, Is.True);
            Assert.That(pagedTree.Children[0].Index, Is.EqualTo(1));
            Assert.That(pagedTree.Children[0].Text, Is.EqualTo("Root 1"));
            Assert.That(pagedMenu.Children[0].Index, Is.EqualTo(1));
            Assert.That(pagedMenu.Children[0].Text, Is.EqualTo("Item 1"));
            Assert.That(dataPage.Metadata["rowScope"].GetString(), Is.EqualTo("data"));
            Assert.That(dataPage.Metadata["totalRowCount"].GetInt32(), Is.EqualTo(3));
            Assert.That(dataRows.Children, Has.Count.EqualTo(1));
            Assert.That(dataRows.Children[0].Index, Is.EqualTo(1));
            Assert.That(dataRows.Children[0].Children.Single(cell => cell.Name == "DeviceName").Text, Is.EqualTo("Switch"));
            Assert.That(dataAction.Name, Is.EqualTo("details"));
            Assert.That(dataAction.State["enabled"].GetBoolean(), Is.False);
            Assert.That(visiblePage.Metadata["rowScope"].GetString(), Is.EqualTo("visible"));
            Assert.That(visibleRows.Children[0].Children.Single(cell => cell.Name == "DeviceName").Text, Is.EqualTo("Firewall"));
            Assert.That(visiblePage.Metadata["sortOrderApplied"].GetBoolean(), Is.True);
            Assert.That(dataPage.Metadata["filterColumns"].EnumerateArray().Select(item => item.GetString()), Does.Contain("DeviceName"));
            Assert.That(fallbackRows.Children, Has.Count.EqualTo(1));
            Assert.That(renderedFallback.Metadata["requestedRowScope"].GetString(), Is.EqualTo("rendered"));
            Assert.That(renderedFallback.Metadata["effectiveRowScope"].GetString(), Is.EqualTo("visible"));
            Assert.That(renderedFallback.Metadata, Does.ContainKey("rowScopeFallback"));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void AntdUIProvider_EnforcesDepthAndPagingBounds() {
        var provider = new AntdUIProvider();
        using var tree = new global::AntdUI.Tree();
        var root = new global::AntdUI.TreeItem("Root");
        var child = new global::AntdUI.TreeItem("Child");
        child.Sub.Add(new global::AntdUI.TreeItem("Grandchild"));
        root.Sub.Add(child);
        tree.Items.Add(root);

        var semantic = provider.Inspect(
            tree,
            CreateContext(maxDepth: 0, maxNodes: 20));

        Assert.Multiple(() => {
            Assert.That(semantic.Children, Has.Count.EqualTo(1));
            Assert.That(semantic.Children[0].Children, Is.Empty);
            Assert.That(semantic.Children[0].Truncated, Is.True);
            Assert.That(semantic.Truncated, Is.True);
        });
    }

    private static ControlProviderContext CreateContext(
        int maxDepth = 4,
        int maxNodes = 20,
        int? start = null,
        int? count = null,
        int? startRow = null,
        int? rowCount = null,
        string? rowScope = null) =>
        new(
            maxDepth: maxDepth,
            maxNodes: maxNodes,
            getControlId: control => control.Name,
            toJsonValue: ToJsonValue,
            start: start,
            count: count,
            startRow: startRow,
            rowCount: rowCount,
            rowScope: rowScope);

    private static JsonElement ToJsonValue(object? value) =>
        JsonSerializer.SerializeToElement(value, SerializerOptions);

    private static void TrySetProperty(object target, string propertyName, object? value) {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
            return;

        var actualType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var converted = actualType.IsEnum && value is string text
            ? Enum.Parse(actualType, text, ignoreCase: true)
            : Convert.ChangeType(value, actualType, CultureInfo.InvariantCulture);
        property.SetValue(target, converted);
    }

    private sealed class DeviceRow {
        public string DeviceName { get; set; } = string.Empty;

        public string IP { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public List<global::AntdUI.ICell> Actions { get; set; } = new();
    }
}