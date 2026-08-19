using System.Globalization;
using System.Reflection;

namespace Rhombus.WinFormsMcp.AntdUI.TestApp;

public sealed class AntdUiInspectionForm : Form {
    public AntdUiInspectionForm() {
        Name = "AntdUiInspectionForm";
        Text = "AntdUI RuntimeBridge Inspection";
        ClientSize = new Size(980, 820);
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel {
            Name = "layoutRoot",
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 12,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var index = 0; index < 12; index++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        var normalButton = new global::AntdUI.Button {
            Name = "antdButtonNormal",
            Text = "Run",
            Width = 180,
            Height = 40
        };
        TrySetProperty(normalButton, "Type", "Primary");
        TrySetProperty(normalButton, "Shape", "Round");
        TrySetProperty(normalButton, "Radius", 8);
        TrySetProperty(normalButton, "IconSvg", "<svg viewBox=\"0 0 16 16\"></svg>");

        var loadingButton = new global::AntdUI.Button {
            Name = "antdButtonLoading",
            Text = "Loading",
            Width = 180,
            Height = 40
        };
        TrySetProperty(loadingButton, "Loading", true);
        TrySetProperty(loadingButton, "ColorScheme", "Dark");

        var input = new global::AntdUI.Input {
            Name = "antdInput",
            Text = "Initial input",
            Width = 260,
            Height = 40
        };
        TrySetProperty(input, "PlaceholderText", "Search devices");
        TrySetProperty(input, "PrefixText", "SN");
        TrySetProperty(input, "SuffixText", "OK");
        TrySetProperty(input, "Status", "Success");
        TrySetProperty(input, "ColorScheme", "Light");

        var readOnlyInput = new global::AntdUI.Input {
            Name = "antdInputReadOnly",
            Text = "Read only",
            Width = 260,
            Height = 40
        };
        TrySetProperty(readOnlyInput, "ReadOnly", true);

        var number = new global::AntdUI.InputNumber {
            Name = "antdInputNumber",
            Width = 180,
            Height = 40
        };
        TrySetProperty(number, "Value", 42m);
        TrySetProperty(number, "Minimum", 0m);
        TrySetProperty(number, "Maximum", 100m);
        TrySetProperty(number, "Increment", 1m);

        var checkboxChecked = new global::AntdUI.Checkbox {
            Name = "antdCheckboxChecked",
            Text = "Checked",
            Checked = true,
            Width = 180,
            Height = 40
        };
        var checkboxUnchecked = new global::AntdUI.Checkbox {
            Name = "antdCheckboxUnchecked",
            Text = "Unchecked",
            Checked = false,
            Width = 180,
            Height = 40
        };

        var radioChecked = new global::AntdUI.Radio {
            Name = "antdRadioChecked",
            Text = "Radio on",
            Checked = true,
            Width = 180,
            Height = 40
        };
        var radioUnchecked = new global::AntdUI.Radio {
            Name = "antdRadioUnchecked",
            Text = "Radio off",
            Checked = false,
            Width = 180,
            Height = 40
        };

        var switchChecked = new global::AntdUI.Switch {
            Name = "antdSwitchChecked",
            Checked = true,
            Width = 120,
            Height = 40
        };
        var switchLoading = new global::AntdUI.Switch {
            Name = "antdSwitchLoading",
            Checked = false,
            Width = 120,
            Height = 40
        };
        TrySetProperty(switchLoading, "Loading", true);

        var select = new global::AntdUI.Select {
            Name = "antdSelect",
            Text = "Beta",
            Width = 260,
            Height = 40
        };
        select.Items.Add(new global::AntdUI.SelectItem("Alpha", "A") { SubText = "First" });
        select.Items.Add(new global::AntdUI.SelectItem("Beta", "B") { SubText = "Second" });
        select.SelectedIndex = 1;

        var tabs = new global::AntdUI.Tabs {
            Name = "antdTabs",
            Width = 420,
            Height = 96
        };
        tabs.Pages.Add(new global::AntdUI.TabPage {
            Name = "antdTabOverview",
            Text = "Overview"
        });
        tabs.Pages.Add(new global::AntdUI.TabPage {
            Name = "antdTabDevices",
            Text = "Devices"
        });
        tabs.SelectedIndex = 1;

        var tree = new global::AntdUI.Tree {
            Name = "antdTree",
            Width = 420,
            Height = 120
        };
        var rootNode = new global::AntdUI.TreeItem("Devices") {
            Name = "treeDevices",
            ID = "devices",
            Checked = true,
            Expand = true
        };
        rootNode.Sub.Add(new global::AntdUI.TreeItem("Router") {
            Name = "treeRouter",
            ID = "router",
            Checked = true
        });
        rootNode.Sub.Add(new global::AntdUI.TreeItem("Switch") {
            Name = "treeSwitch",
            ID = "switch"
        });
        tree.Items.Add(rootNode);
        tree.SelectItem = rootNode.Sub[0];

        var table = new global::AntdUI.Table {
            Name = "antdTable",
            Width = 420,
            Height = 136,
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
            }
        };
        table.Columns.Add(new global::AntdUI.Column("DeviceName", "Device Name"));
        table.Columns.Add(new global::AntdUI.Column("IP", "IP"));
        table.Columns.Add(new global::AntdUI.Column("Status", "Status"));
        table.Columns.Add(new global::AntdUI.Column("Actions", "Actions"));
        table.SelectedIndex = 1;

        var menu = new global::AntdUI.Menu {
            Name = "antdMenu",
            Width = 420,
            Height = 120
        };
        var fileMenu = new global::AntdUI.MenuItem("File") {
            Name = "menuFile",
            ID = "file",
            Expand = true
        };
        fileMenu.Sub.Add(new global::AntdUI.MenuItem("Open") {
            Name = "menuOpen",
            ID = "open"
        });
        fileMenu.Sub.Add(new global::AntdUI.MenuItem("Exit") {
            Name = "menuExit",
            ID = "exit",
            Enabled = false
        });
        menu.Items.Add(fileMenu);

        Add(layout, normalButton, 0, 0);
        Add(layout, loadingButton, 1, 0);
        Add(layout, input, 0, 1);
        Add(layout, readOnlyInput, 1, 1);
        Add(layout, number, 0, 2);
        Add(layout, checkboxChecked, 0, 3);
        Add(layout, checkboxUnchecked, 1, 3);
        Add(layout, radioChecked, 0, 4);
        Add(layout, radioUnchecked, 1, 4);
        Add(layout, switchChecked, 0, 5);
        Add(layout, switchLoading, 1, 5);
        Add(layout, select, 0, 6);
        Add(layout, tabs, 0, 7);
        Add(layout, tree, 1, 7);
        Add(layout, table, 0, 9);
        Add(layout, menu, 1, 9);

        Controls.Add(layout);
    }

    private sealed class DeviceRow {
        public string DeviceName { get; set; } = string.Empty;

        public string IP { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public List<global::AntdUI.ICell> Actions { get; set; } = new();
    }

    private static void Add(TableLayoutPanel layout, Control control, int column, int row) {
        control.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        control.Margin = new Padding(8);
        layout.Controls.Add(control, column, row);
    }

    private static void TrySetProperty(object target, string propertyName, object? value) {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
            return;

        try {
            var converted = ConvertValue(value, property.PropertyType);
            property.SetValue(target, converted);
        }
        catch {
            // The test app should keep running if an AntdUI version changes an optional visual property.
        }
    }

    private static object? ConvertValue(object? value, Type targetType) {
        if (value is null)
            return null;
        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (actualType.IsInstanceOfType(value))
            return value;
        if (actualType.IsEnum && value is string enumText)
            return Enum.Parse(actualType, enumText, ignoreCase: true);
        return Convert.ChangeType(value, actualType, CultureInfo.InvariantCulture);
    }
}