using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

internal sealed class StandardWinFormsProvider : IControlProvider {
    public string ProviderName => "StandardWinForms";

    public int Priority => 0;

    public bool CanHandle(Control control) => control is not null;

    public ControlProviderSnapshot Describe(Control control) => new() {
        ProviderName = ProviderName,
        Priority = Priority,
        RuntimeType = GetRuntimeType(control),
        SemanticType = GetSemanticType(control),
        ProviderVersion = typeof(Control).Assembly.GetName().Version?.ToString(),
        Capabilities = [
            "identity",
            "state",
            "properties",
            "layout",
            "bindings",
            "standardSemantic"
        ]
    };

    public ControlSemanticSnapshot Inspect(Control control, ControlProviderContext context) {
        var semantic = new ControlSemanticSnapshot {
            ProviderName = ProviderName,
            RuntimeType = GetRuntimeType(control),
            SemanticType = GetSemanticType(control),
            ChildCount = TryGet(() => control.Controls.Count, 0)
        };

        AddState(semantic, context, "text", TryGet(() => control.Text, string.Empty));
        AddState(semantic, context, "visible", TryGet(() => control.Visible, false));
        AddState(semantic, context, "enabled", TryGet(() => control.Enabled, false));
        AddState(semantic, context, "focused", TryGet(() => control.Focused, false));
        AddCommonProperties(semantic, control, context);
        semantic.SupportedInteractionHints.AddRange(GetInteractionHints(control));
        AddSemanticChildren(semantic, control, context);
        return semantic;
    }

    private static void AddCommonProperties(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context) {
        switch (control) {
            case CheckBox checkBox:
                AddState(semantic, context, "checked", TryGet(() => checkBox.Checked, false));
                AddState(semantic, context, "checkState", TryGet(() => checkBox.CheckState.ToString(), string.Empty));
                break;
            case RadioButton radioButton:
                AddState(semantic, context, "checked", TryGet(() => radioButton.Checked, false));
                break;
            case TextBoxBase textBox:
                AddState(semantic, context, "readOnly", TryGet(() => textBox.ReadOnly, false));
                AddProperty(semantic, context, "multiline", TryGet(() => textBox.Multiline, false));
                break;
            case ComboBox comboBox:
                AddState(semantic, context, "selectedIndex", TryGet(() => comboBox.SelectedIndex, -1));
                AddProperty(semantic, context, "itemCount", TryGet(() => comboBox.Items.Count, 0));
                AddProperty(semantic, context, "dropDownStyle", TryGet(() => comboBox.DropDownStyle.ToString(), string.Empty));
                break;
            case ListBox listBox:
                AddState(semantic, context, "selectedIndex", TryGet(() => listBox.SelectedIndex, -1));
                AddProperty(semantic, context, "itemCount", TryGet(() => listBox.Items.Count, 0));
                break;
            case TreeView treeView:
                AddProperty(semantic, context, "nodeCount", TryGet(() => treeView.Nodes.Count, 0));
                break;
            case DataGridView grid:
                AddProperty(semantic, context, "columnCount", TryGet(() => grid.Columns.Count, 0));
                AddProperty(semantic, context, "rowCount", TryGet(() => grid.Rows.Count, 0));
                AddProperty(semantic, context, "selectedRowCount", TryGet(() => grid.SelectedRows.Count, 0));
                break;
            case TabControl tabs:
                AddState(semantic, context, "selectedIndex", TryGet(() => tabs.SelectedIndex, -1));
                AddProperty(semantic, context, "tabCount", TryGet(() => tabs.TabPages.Count, 0));
                break;
            case MenuStrip menu:
                AddProperty(semantic, context, "itemCount", TryGet(() => menu.Items.Count, 0));
                break;
            case Form form:
                AddState(semantic, context, "windowState", TryGet(() => form.WindowState.ToString(), string.Empty));
                AddProperty(semantic, context, "isMdiContainer", TryGet(() => form.IsMdiContainer, false));
                break;
        }
    }

    private static void AddSemanticChildren(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context) {
        if (context.MaxNodes <= 0)
            return;

        switch (control) {
            case TabControl tabs:
                AddTabPages(semantic, tabs, context);
                break;
            case MenuStrip menu:
                AddMenuItems(semantic, menu, context);
                break;
        }
    }

    private static void AddTabPages(
        ControlSemanticSnapshot semantic,
        TabControl tabs,
        ControlProviderContext context) {
        var tabCount = TryGet(() => tabs.TabPages.Count, 0);
        semantic.ChildCount = tabCount;
        for (var index = 0; index < tabCount; index++) {
            if (semantic.Children.Count >= context.MaxNodes) {
                semantic.Truncated = true;
                break;
            }

            var page = tabs.TabPages[index];
            semantic.Children.Add(new SemanticNodeSnapshot {
                Kind = "tab-page",
                Name = TryGet(() => page.Name, string.Empty),
                Text = TryGet(() => page.Text, string.Empty),
                Index = index,
                ControlId = context.GetControlId(page),
                ChildCount = TryGet(() => page.Controls.Count, 0),
                State = {
                    ["selected"] = context.ToJsonValue(index == TryGet(() => tabs.SelectedIndex, -1)),
                    ["enabled"] = context.ToJsonValue(TryGet(() => page.Enabled, false)),
                    ["visible"] = context.ToJsonValue(TryGet(() => page.Visible, false))
                }
            });
        }
    }

    private static void AddMenuItems(
        ControlSemanticSnapshot semantic,
        MenuStrip menu,
        ControlProviderContext context) {
        var itemCount = TryGet(() => menu.Items.Count, 0);
        var remainingNodes = context.MaxNodes;
        semantic.ChildCount = itemCount;
        for (var index = 0; index < itemCount; index++) {
            if (remainingNodes <= 0) {
                semantic.Truncated = true;
                break;
            }

            var node = BuildMenuNode(menu.Items[index], index, context, 0, ref remainingNodes);
            if (node.Truncated)
                semantic.Truncated = true;
            semantic.Children.Add(node);
        }
    }

    private static SemanticNodeSnapshot BuildMenuNode(
        ToolStripItem item,
        int index,
        ControlProviderContext context,
        int depth,
        ref int remainingNodes) {
        remainingNodes--;
        var node = new SemanticNodeSnapshot {
            Kind = "menu-item",
            Name = TryGet(() => item.Name, string.Empty),
            Text = TryGet(() => item.Text, string.Empty),
            Index = index,
            State = {
                ["enabled"] = context.ToJsonValue(TryGet(() => item.Enabled, false)),
                ["visible"] = context.ToJsonValue(TryGet(() => item.Visible, false))
            }
        };

        if (item is ToolStripMenuItem menuItem) {
            node.State["checked"] = context.ToJsonValue(TryGet(() => menuItem.Checked, false));
            node.ChildCount = TryGet(() => menuItem.DropDownItems.Count, 0);
            if (depth >= context.MaxDepth)
                node.Truncated = node.ChildCount > 0;
            else {
                for (var childIndex = 0; childIndex < node.ChildCount; childIndex++) {
                    if (remainingNodes <= 0) {
                        node.Truncated = true;
                        break;
                    }

                    node.Children.Add(BuildMenuNode(
                        menuItem.DropDownItems[childIndex],
                        childIndex,
                        context,
                        depth + 1,
                        ref remainingNodes));
                }
            }
        }

        return node;
    }

    private static string GetRuntimeType(Control control) =>
        control.GetType().FullName ?? control.GetType().Name;

    private static string GetSemanticType(Control control) => control switch {
        Form => "form",
        CheckBox => "checkbox",
        RadioButton => "radio",
        Button => "button",
        TextBoxBase => "text-input",
        ComboBox => "select",
        ListBox => "list",
        TreeView => "tree",
        DataGridView => "table",
        TabControl => "tabs",
        MenuStrip => "menu",
        Label => "text",
        Panel or GroupBox or FlowLayoutPanel or TableLayoutPanel or SplitContainer => "container",
        _ when control.GetType().Assembly == typeof(Control).Assembly => "control",
        _ => "custom-control"
    };

    private static IEnumerable<string> GetInteractionHints(Control control) => control switch {
        Button => ["invoke"],
        CheckBox => ["toggle"],
        RadioButton => ["select"],
        TextBoxBase => ["text-input"],
        ComboBox => ["select"],
        ListBox => ["select"],
        TreeView => ["expand-collapse", "select"],
        DataGridView => ["table-navigation"],
        TabControl => ["select-tab"],
        MenuStrip => ["menu-navigation"],
        _ => []
    };

    private static void AddState(
        ControlSemanticSnapshot semantic,
        ControlProviderContext context,
        string name,
        object? value) =>
        semantic.State[name] = context.ToJsonValue(value);

    private static void AddProperty(
        ControlSemanticSnapshot semantic,
        ControlProviderContext context,
        string name,
        object? value) =>
        semantic.Properties[name] = context.ToJsonValue(value);

    private static T TryGet<T>(Func<T> callback, T fallback) {
        try {
            return callback();
        }
        catch {
            return fallback;
        }
    }
}