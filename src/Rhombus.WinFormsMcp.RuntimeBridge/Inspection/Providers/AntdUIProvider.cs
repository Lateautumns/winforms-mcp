using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

internal sealed partial class AntdUIProvider : IControlProvider {
    private const string NamespacePrefix = "AntdUI.";
    private const int MaximumNonIndexedPageOffset = 10_000;

    private static readonly IReadOnlyDictionary<string, ControlProfile> Profiles =
        new Dictionary<string, ControlProfile>(StringComparer.Ordinal) {
            ["AntdUI.Button"] = new(
                "button",
                ["invoke"],
                [
                    Read.State("Loading", "loading"),
                    Read.State("Toggle", "toggle")
                ],
                [
                    Read.Property("Type", "type"),
                    Read.Property("Shape", "shape"),
                    Read.Property("Radius", "radius"),
                    Read.Property("Ghost", "ghost"),
                    Read.Property("AutoToggle", "autoToggle"),
                    Read.Property("ToggleText", "toggleText"),
                    Read.Property("IconSvg", "iconSvg"),
                    Read.Property("LoadingSvg", "loadingSvg"),
                    Read.Property("LoadingRespondClick", "loadingRespondClick"),
                    Read.Property("DialogResult", "dialogResult")
                ]),
            ["AntdUI.Input"] = new(
                "textbox",
                ["text-input"],
                [
                    Read.State("ReadOnly", "readOnly")
                ],
                [
                    Read.Property("PlaceholderText", "placeholderText"),
                    Read.Property("PrefixText", "prefixText"),
                    Read.Property("PrefixSvg", "prefixSvg"),
                    Read.Property("SuffixText", "suffixText"),
                    Read.Property("SuffixSvg", "suffixSvg"),
                    Read.Property("Status", "status"),
                    Read.Property("Radius", "radius"),
                    Read.Property("Multiline", "multiline"),
                    Read.Property("AcceptsTab", "acceptsTab"),
                    Read.Property("AcceptsEscape", "acceptsEscape"),
                    Read.Property("HideSelection", "hideSelection"),
                    Read.Property("UseContextMenu", "useContextMenu"),
                    Read.Property("MaxLength", "maxLength"),
                    Read.Property("VirtualMode", "virtualMode")
                ]),
            ["AntdUI.InputNumber"] = new(
                "number-input",
                ["text-input", "numeric"],
                [
                    Read.State("ReadOnly", "readOnly"),
                    Read.State("Value", "value")
                ],
                [
                    Read.Property("Minimum", "minimum"),
                    Read.Property("Maximum", "maximum"),
                    Read.Property("Increment", "increment"),
                    Read.Property("AlwaysShowControl", "alwaysShowControl"),
                    Read.Property("InterceptArrowKeys", "interceptArrowKeys"),
                    Read.Property("EnabledValueTextChange", "enabledValueTextChange")
                ]),
            ["AntdUI.Checkbox"] = new(
                "checkbox",
                ["toggle"],
                [
                    Read.State("Checked", "checked")
                ],
                [
                    Read.Property("AutoCheck", "autoCheck")
                ]),
            ["AntdUI.Radio"] = new(
                "radio",
                ["select"],
                [
                    Read.State("Checked", "checked")
                ],
                [
                    Read.Property("AutoCheck", "autoCheck")
                ]),
            ["AntdUI.Switch"] = new(
                "switch",
                ["toggle"],
                [
                    Read.State("Checked", "checked"),
                    Read.State("Loading", "loading")
                ],
                [
                    Read.Property("AutoCheck", "autoCheck")
                ]),
            ["AntdUI.Select"] = new(
                "select",
                ["select"],
                [
                    Read.State("SelectedIndex", "selectedIndex"),
                    Read.State("SelectedValue", "selectedValue")
                ],
                [
                    Read.Property("List", "list"),
                    Read.Property("Placement", "placement"),
                    Read.Property("MaxCount", "maxCount"),
                    Read.Property("AutoPrefixSvg", "autoPrefixSvg")
                ],
                includeItems: true),
            ["AntdUI.Tabs"] = new(
                "tabs",
                ["select-tab"],
                [
                    Read.State("SelectedIndex", "selectedIndex")
                ],
                [
                    Read.Property("Pages", "pages")
                ]),
            ["AntdUI.Tree"] = new(
                "tree",
                ["expand-collapse", "select"],
                [
                    Read.State("Multiple", "multiple")
                ],
                [
                    Read.Property("VirtualMode", "virtualMode"),
                    Read.Property("Checkable", "checkable")
                ]),
            ["AntdUI.Table"] = new(
                "table",
                ["table-navigation"],
                [
                    Read.State("SelectedIndex", "selectedIndex")
                ],
                [
                    Read.Property("VirtualMode", "virtualMode"),
                    Read.Property("VisibleHeader", "visibleHeader"),
                    Read.Property("FixedHeader", "fixedHeader"),
                    Read.Property("AutoSizeColumnsMode", "autoSizeColumnsMode")
                ]),
            ["AntdUI.Menu"] = new(
                "menu",
                ["menu-navigation"],
                [],
                [
                    Read.Property("Mode", "mode"),
                    Read.Property("Collapsed", "collapsed"),
                    Read.Property("Unique", "unique")
                ])
        };

    private static readonly Read[] CommonReads = [
        Read.Property("ColorScheme", "colorScheme"),
        Read.Property("Dpi", "dpi"),
        Read.Property("HandCursor", "handCursor")
    ];

    public string ProviderName => "AntdUI";

    public int Priority => 100;

    public bool CanHandle(Control control) => IsAntdUIControl(control.GetType());

    public ControlProviderSnapshot Describe(Control control) {
        var runtimeType = GetRuntimeType(control);
        var profile = ResolveProfile(control.GetType());
        return new ControlProviderSnapshot {
            ProviderName = ProviderName,
            Priority = Priority,
            RuntimeType = runtimeType,
            SemanticType = profile.SemanticType,
            ProviderVersion = control.GetType().Assembly.GetName().Version?.ToString(),
            Capabilities = [
                "antduiBasicSemantics",
                "antduiComplexSemantics",
                "semanticPaging",
                "tableRowScopes",
                "identity",
                "state",
                "properties",
                "layout",
                "bindings"
            ]
        };
    }

    public ControlSemanticSnapshot Inspect(Control control, ControlProviderContext context) {
        var profile = ResolveProfile(control.GetType());
        var semantic = new ControlSemanticSnapshot {
            ProviderName = ProviderName,
            RuntimeType = GetRuntimeType(control),
            SemanticType = profile.SemanticType,
            ChildCount = TryGet(() => control.Controls.Count, 0),
            SupportedInteractionHints = profile.InteractionHints.ToList()
        };

        AddState(semantic, context, "text", TryGet(() => control.Text, string.Empty));
        AddState(semantic, context, "visible", TryGet(() => control.Visible, false));
        AddState(semantic, context, "enabled", TryGet(() => control.Enabled, false));
        AddState(semantic, context, "focused", TryGet(() => control.Focused, false));
        AddMetadata(semantic, context, "providerVersion", control.GetType().Assembly.GetName().Version?.ToString());

        foreach (var read in CommonReads.Concat(profile.Reads))
            AddRead(semantic, control, context, read);

        if (profile.IncludeItems)
            AddSelectItems(semantic, control, context);
        AddComplexSemanticChildren(semantic, control, context);

        return semantic;
    }

    private static void AddComplexSemanticChildren(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context) {
        switch (GetRuntimeType(control)) {
            case "AntdUI.Tabs":
                AddTabsPages(semantic, control, context);
                break;
            case "AntdUI.Tree":
                AddHierarchicalItems(
                    semantic,
                    control,
                    context,
                    collectionProperty: "Items",
                    childCollectionProperty: "Sub",
                    itemKind: "tree-node",
                    selectedItemProperty: "SelectItem");
                break;
            case "AntdUI.Table":
                AddTableSemantic(semantic, control, context);
                break;
            case "AntdUI.Menu":
                AddHierarchicalItems(
                    semantic,
                    control,
                    context,
                    collectionProperty: "Items",
                    childCollectionProperty: "Sub",
                    itemKind: "menu-item",
                    selectedItemProperty: null);
                break;
        }
    }

    private static void AddTabsPages(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context) {
        if (!TryReadEnumerableProperty(control, "Pages", semantic, out var pages))
            return;

        var selectedIndex = TryReadIntProperty(control, "SelectedIndex");
        var count = TryGetCollectionCount(pages);
        if (count.HasValue) {
            semantic.ChildCount = count.Value;
            semantic.Properties["pageCount"] = context.ToJsonValue(count.Value);
        }

        var pageResult = ReadCollectionPage(pages, context.Start, context.Count, context.MaxNodes);
        AddCollectionPagingMetadata(semantic, context, pageResult, count);
        foreach (var page in pageResult.Items) {

            var node = new SemanticNodeSnapshot {
                Kind = "tab-page",
                Name = page.Value is null ? null : TryReadStringProperty(page.Value, "Name"),
                Text = page.Value is null ? null : TryReadStringProperty(page.Value, "Text"),
                Value = page.Value is null ? null : TryReadStringProperty(page.Value, "Name"),
                Index = page.Index,
                ControlId = page.Value is Control pageControl ? context.GetControlId(pageControl) : null,
                ChildCount = page.Value is Control pageControlForChildren
                    ? TryGet(() => pageControlForChildren.Controls.Count, 0)
                    : 0
            };
            node.State["selected"] = context.ToJsonValue(selectedIndex == page.Index);
            AddNodeState(node, page.Value, context, "Enabled", "enabled");
            AddNodeState(node, page.Value, context, "Visible", "visible");
            AddNodeState(node, page.Value, context, "ReadOnly", "readOnly");
            AddNodeProperty(node, page.Value, context, "IconSvg", "iconSvg");
            AddNodeProperty(node, page.Value, context, "Badge", "badge");
            AddRuntimeType(node, page.Value, context);
            semantic.Children.Add(node);
        }
    }

    private static void AddHierarchicalItems(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context,
        string collectionProperty,
        string childCollectionProperty,
        string itemKind,
        string? selectedItemProperty) {
        if (!TryReadEnumerableProperty(control, collectionProperty, semantic, out var items))
            return;

        var selectedItem = selectedItemProperty is null
            ? null
            : TryReadProperty(control, selectedItemProperty, out var value, out _) ? value : null;
        var remainingNodes = context.MaxNodes;
        var count = TryGetCollectionCount(items);
        if (count.HasValue)
            semantic.ChildCount = count.Value;

        var pageResult = ReadCollectionPage(items, context.Start, context.Count, context.MaxNodes);
        AddCollectionPagingMetadata(semantic, context, pageResult, count);
        foreach (var item in pageResult.Items) {
            if (remainingNodes <= 0) {
                semantic.Truncated = true;
                break;
            }

            var node = BuildHierarchicalItem(
                item.Value,
                item.Index,
                context,
                itemKind,
                childCollectionProperty,
                selectedItem,
                depth: 0,
                ref remainingNodes);
            if (node.Truncated)
                semantic.Truncated = true;
            semantic.Children.Add(node);
        }
    }

    private static SemanticNodeSnapshot BuildHierarchicalItem(
        object? item,
        int index,
        ControlProviderContext context,
        string itemKind,
        string childCollectionProperty,
        object? selectedItem,
        int depth,
        ref int remainingNodes) {
        remainingNodes--;
        var node = new SemanticNodeSnapshot {
            Kind = itemKind,
            Index = index
        };

        if (item is null)
            return node;

        node.Name = TryReadStringProperty(item, "Name");
        node.Text = TryReadStringProperty(item, "Text") ?? ToSafeDisplayString(item);
        node.Value = TryReadStringProperty(item, "ID") ??
            TryReadStringProperty(item, "Name") ??
            TryReadStringProperty(item, "Tag");
        AddRuntimeType(node, item, context);
        AddNodeProperty(node, item, context, "ID", "id");
        AddNodeProperty(node, item, context, "SubText", "subText");
        AddNodeProperty(node, item, context, "SubTitle", "subTitle");
        AddNodeProperty(node, item, context, "IconSvg", "iconSvg");
        AddNodeState(node, item, context, "Enabled", "enabled");
        AddNodeState(node, item, context, "Visible", "visible");
        AddNodeState(node, item, context, "Checked", "checked");
        AddNodeState(node, item, context, "CheckState", "checkState");
        AddNodeState(node, item, context, "Expand", "expanded");
        AddNodeState(node, item, context, "Loading", "loading");
        node.State["selected"] = context.ToJsonValue(selectedItem is not null && ReferenceEquals(item, selectedItem));

        if (!TryReadProperty(item, childCollectionProperty, out var children, out _) || children is not IEnumerable enumerable)
            return node;

        var count = TryGetCollectionCount(children);
        if (count.HasValue)
            node.ChildCount = count.Value;
        if (depth >= context.MaxDepth) {
            node.Truncated = node.ChildCount > 0;
            return node;
        }

        var childIndex = 0;
        foreach (var child in enumerable) {
            if (remainingNodes <= 0) {
                node.Truncated = true;
                break;
            }

            var childNode = BuildHierarchicalItem(
                child,
                childIndex,
                context,
                itemKind,
                childCollectionProperty,
                selectedItem,
                depth + 1,
                ref remainingNodes);
            if (childNode.Truncated)
                node.Truncated = true;
            node.Children.Add(childNode);
            childIndex++;
        }

        return node;
    }

    private static void AddTableSemantic(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context) {
        var selectedIndexes = TryReadSelectedIndexes(control);
        semantic.Metadata["selectedIndexes"] = SerializeSafe(selectedIndexes.OrderBy(index => index).ToArray());
        AddMetadata(semantic, context, "selectionIndexBase", 1);
        AddMetadata(semantic, context, "startRow", context.StartRow);

        var remainingNodes = context.MaxNodes;
        var columnsNode = BuildTableColumnsNode(control, context, ref remainingNodes, out var columns);
        if (columnsNode.Truncated)
            semantic.Truncated = true;
        semantic.Children.Add(columnsNode);

        var scope = ResolveTableScope(control, context.RowScope);
        AddTableScopeMetadata(semantic, context, scope);
        AddTableStateMetadata(semantic, context, columns, scope);
        var rowsNode = BuildTableRowsNode(context, columns, selectedIndexes, scope, ref remainingNodes);
        if (rowsNode.Truncated)
            semantic.Truncated = true;
        semantic.Children.Add(rowsNode);
        semantic.ChildCount = semantic.Children.Count;
    }

    private static SemanticNodeSnapshot BuildTableColumnsNode(
        Control control,
        ControlProviderContext context,
        ref int remainingNodes,
        out List<TableColumnInfo> columnInfos) {
        columnInfos = new List<TableColumnInfo>();
        var node = new SemanticNodeSnapshot {
            Kind = "columns"
        };

        if (!TryReadProperty(control, "Columns", out var columns, out var error)) {
            if (!string.IsNullOrEmpty(error))
                node.Properties["error"] = context.ToJsonValue(error);
            return node;
        }

        var count = TryGetCollectionCount(columns!);
        if (count.HasValue)
            node.ChildCount = count.Value;
        if (columns is not IEnumerable enumerable)
            return node;

        var index = 0;
        foreach (var column in enumerable) {
            if (remainingNodes <= 0) {
                node.Truncated = true;
                break;
            }

            remainingNodes--;
            var columnInfo = BuildTableColumnNode(column, index, context);
            columnInfos.Add(columnInfo);
            node.Children.Add(columnInfo.Node);
            index++;
        }

        if (count.HasValue && index < count.Value)
            node.Truncated = true;

        return node;
    }

    private static TableColumnInfo BuildTableColumnNode(
        object? column,
        int index,
        ControlProviderContext context) {
        var node = new SemanticNodeSnapshot {
            Kind = "column",
            Index = index
        };
        if (column is null)
            return new TableColumnInfo(column, index, string.Empty, null, true, node);

        var key = TryReadStringProperty(column, "Key");
        var title = TryReadStringProperty(column, "Title") ?? key;
        var visible = TryReadBooleanProperty(column, "Visible") ?? true;
        node.Name = key;
        node.Text = title;
        node.Value = key;
        AddRuntimeType(node, column, context);
        AddNodeProperty(node, column, context, "Key", "key");
        AddNodeProperty(node, column, context, "Title", "title");
        AddNodeProperty(node, column, context, "Width", "width");
        AddNodeProperty(node, column, context, "Align", "align");
        AddNodeProperty(node, column, context, "ColAlign", "headerAlign");
        AddNodeProperty(node, column, context, "VisibleIndex", "visibleIndex");
        AddNodeProperty(node, column, context, "ReadOnly", "readOnly");
        AddNodeProperty(node, column, context, "Editable", "editable");
        AddNodeProperty(node, column, context, "DisplayFormat", "displayFormat");
        AddNodeProperty(node, column, context, "SortOrder", "sortOrder");
        AddNodeProperty(node, column, context, "SortMode", "sortMode");
        AddNodeProperty(node, column, context, "HasFilter", "hasFilter");
        AddNodeProperty(node, column, context, "KeyTree", "treeKey");
        AddNodeState(node, column, context, "Visible", "visible");
        AddColumnFilterMetadata(node, column, context);
        if (TryReadProperty(column, "Render", out var render, out _))
            node.Properties["hasRender"] = context.ToJsonValue(render is Delegate);
        return new TableColumnInfo(column, index, key ?? string.Empty, title, visible, node);
    }

    private static SemanticNodeSnapshot BuildTableRowsNode(
        ControlProviderContext context,
        IReadOnlyList<TableColumnInfo> columns,
        ISet<int> selectedIndexes,
        TableScopeResult scope,
        ref int remainingNodes) {
        var node = new SemanticNodeSnapshot {
            Kind = "rows",
            ChildCount = scope.TotalCount ?? 0
        };
        node.Properties["scope"] = context.ToJsonValue(scope.EffectiveScope);
        node.Properties["startRow"] = context.ToJsonValue(context.StartRow);
        var rowLimit = Math.Min(context.RowCount ?? DefaultTableRowCount, context.MaxNodes);
        node.Properties["rowLimit"] = context.ToJsonValue(rowLimit);

        var page = ReadTableRowPage(scope, context.StartRow, rowLimit);
        if (page.OffsetLimited) {
            node.Properties["pagingWarning"] = context.ToJsonValue(
                "The requested row offset exceeds the bounded non-indexed collection scan limit.");
            node.Truncated = true;
        }
        foreach (var row in page.Items) {
            if (remainingNodes <= 0) {
                node.Truncated = true;
                break;
            }

            remainingNodes--;
            var rowNode = BuildTableRowNode(row, columns, selectedIndexes, context, ref remainingNodes);
            if (rowNode.Truncated)
                node.Truncated = true;
            node.Children.Add(rowNode);
        }

        if (page.HasMore)
            node.Truncated = true;
        if (!scope.TotalCount.HasValue)
            node.Properties["totalCountKnown"] = context.ToJsonValue(false);

        return node;
    }

    private static SemanticNodeSnapshot BuildTableRowNode(
        TableRowInfo row,
        IReadOnlyList<TableColumnInfo> columns,
        ISet<int> selectedIndexes,
        ControlProviderContext context,
        ref int remainingNodes) {
        var node = new SemanticNodeSnapshot {
            Kind = "row",
            Index = row.SourceIndex,
            ChildCount = columns.Count
        };
        node.State["selected"] = context.ToJsonValue(selectedIndexes.Contains(row.ViewIndex + 1));
        node.State["enabled"] = context.ToJsonValue(row.Enabled);
        node.Properties["viewIndex"] = context.ToJsonValue(row.ViewIndex);
        node.Properties["sourceIndex"] = context.ToJsonValue(row.SourceIndex);
        node.Properties["scope"] = context.ToJsonValue(row.Scope);
        if (row.Depth.HasValue)
            node.Properties["depth"] = context.ToJsonValue(row.Depth.Value);
        if (row.Expanded.HasValue)
            node.State["expanded"] = context.ToJsonValue(row.Expanded.Value);
        AddRuntimeType(node, row.Record, context);

        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++) {
            if (remainingNodes <= 0) {
                node.Truncated = true;
                break;
            }

            remainingNodes--;
            var column = columns[columnIndex];
            var cell = BuildTableCellNode(row, column, columnIndex, context, ref remainingNodes);
            if (cell.Truncated)
                node.Truncated = true;
            node.Children.Add(cell);
        }

        return node;
    }

    private static void AddSelectItems(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context) {
        if (!TryReadProperty(control, "Items", out var items, out var error)) {
            if (!string.IsNullOrEmpty(error))
                semantic.Errors["Items"] = error!;
            return;
        }

        if (items is not IEnumerable enumerable)
            return;

        var count = TryGetCollectionCount(items);
        if (count.HasValue)
            semantic.ChildCount = count.Value;

        var page = ReadCollectionPage(enumerable, context.Start, context.Count, context.MaxNodes);
        AddCollectionPagingMetadata(semantic, context, page, count);
        foreach (var item in page.Items)
            semantic.Children.Add(BuildSelectItem(item.Value, item.Index, context));
    }

    private static void AddCollectionPagingMetadata(
        ControlSemanticSnapshot semantic,
        ControlProviderContext context,
        CollectionPage page,
        int? totalCount) {
        AddMetadata(semantic, context, "start", context.Start);
        if (context.Count.HasValue)
            AddMetadata(semantic, context, "count", context.Count.Value);
        if (totalCount.HasValue)
            AddMetadata(semantic, context, "totalCount", totalCount.Value);
        else
            AddMetadata(semantic, context, "totalCountKnown", false);

        if (page.HasMore || (totalCount.HasValue && context.Start > 0 && totalCount.Value > 0))
            semantic.Truncated = true;
        if (page.OffsetLimited)
            semantic.Errors["paging"] = "The requested offset exceeds the bounded non-indexed collection scan limit.";
    }

    private static CollectionPage ReadCollectionPage(
        IEnumerable collection,
        int start,
        int? requestedCount,
        int maximumCount) {
        var result = new CollectionPage();
        var limit = Math.Min(Math.Max(0, requestedCount ?? maximumCount), maximumCount);
        if (limit == 0) {
            result.HasMore = TryGetCollectionCount(collection) is int totalCount && start < totalCount;
            return result;
        }

        if (collection is IList indexed) {
            var end = Math.Min(indexed.Count, SafeAdd(start, limit));
            for (var index = start; index < end; index++)
                result.Items.Add(new IndexedItem(index, indexed[index]));
            result.HasMore = end < indexed.Count;
            return result;
        }

        if (start > MaximumNonIndexedPageOffset) {
            result.OffsetLimited = true;
            result.HasMore = TryGetCollectionCount(collection) is int totalCount && start < totalCount;
            return result;
        }

        var indexFromEnumeration = 0;
        foreach (var item in collection) {
            if (indexFromEnumeration++ < start)
                continue;
            if (result.Items.Count >= limit) {
                result.HasMore = true;
                break;
            }

            result.Items.Add(new IndexedItem(indexFromEnumeration - 1, item));
        }

        return result;
    }

    private static int SafeAdd(int left, int right) =>
        left > int.MaxValue - right ? int.MaxValue : left + right;

    private static SemanticNodeSnapshot BuildSelectItem(
        object? item,
        int index,
        ControlProviderContext context) {
        if (item is null) {
            return new SemanticNodeSnapshot {
                Kind = "select-item",
                Index = index
            };
        }

        var text = TryReadStringProperty(item, "Text") ?? ToSafeDisplayString(item);
        var value = TryReadStringProperty(item, "Value") ??
            TryReadStringProperty(item, "Tag") ??
            TryReadStringProperty(item, "SubText");
        var node = new SemanticNodeSnapshot {
            Kind = "select-item",
            Text = text,
            Value = value,
            Index = index,
            Properties = {
                ["runtimeType"] = context.ToJsonValue(item.GetType().FullName)
            }
        };

        AddItemProperty(node, item, context, "SubText", "subText");
        AddItemProperty(node, item, context, "IconSvg", "iconSvg");
        AddItemProperty(node, item, context, "Online", "online");
        return node;
    }

    private static void AddItemProperty(
        SemanticNodeSnapshot node,
        object item,
        ControlProviderContext context,
        string propertyName,
        string key) {
        if (TryReadProperty(item, propertyName, out var value, out _) && value is not null)
            node.Properties[key] = context.ToJsonValue(SanitizeValue(value));
    }

    private static void AddNodeProperty(
        SemanticNodeSnapshot node,
        object? item,
        ControlProviderContext context,
        string propertyName,
        string key) {
        if (item is not null && TryReadProperty(item, propertyName, out var value, out _) && value is not null)
            node.Properties[key] = context.ToJsonValue(SanitizeValue(value));
    }

    private static void AddNodeState(
        SemanticNodeSnapshot node,
        object? item,
        ControlProviderContext context,
        string propertyName,
        string key) {
        if (item is not null && TryReadProperty(item, propertyName, out var value, out _) && value is not null)
            node.State[key] = context.ToJsonValue(SanitizeValue(value));
    }

    private static void AddRuntimeType(
        SemanticNodeSnapshot node,
        object? item,
        ControlProviderContext context) {
        if (item is not null)
            node.Properties["runtimeType"] = context.ToJsonValue(item.GetType().FullName);
    }

    private static void AddRead(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context,
        Read read) {
        if (!TryReadProperty(control, read.PropertyName, out var value, out var error)) {
            if (!string.IsNullOrEmpty(error))
                semantic.Errors[read.PropertyName] = error!;
            return;
        }

        var target = read.Target == ReadTarget.State ? semantic.State : semantic.Properties;
        target[read.Key] = context.ToJsonValue(SanitizeValue(value));
    }

    private static object? SanitizeValue(object? value) {
        if (value is null)
            return null;
        var type = value.GetType();
        if (type.IsPrimitive || value is string or decimal)
            return value;
        if (value is Enum)
            return value.ToString();
        if (value is IEnumerable && value is not string)
            return value.ToString();
        return value.ToString();
    }

    private static bool TryReadProperty(
        object target,
        string propertyName,
        out object? value,
        out string? error) {
        value = null;
        error = null;
        var property = FindPublicProperty(target.GetType(), propertyName);
        if (property is null)
            return false;
        if (property.GetIndexParameters().Length > 0)
            return false;

        try {
            value = property.GetValue(target);
            return true;
        }
        catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }

    private static PropertyInfo? FindPublicProperty(Type type, string propertyName) {
        for (var current = type; current is not null; current = current.BaseType) {
            var property = current.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (property is not null)
                return property;
        }

        return null;
    }

    private static string? TryReadStringProperty(object target, string propertyName) {
        if (!TryReadProperty(target, propertyName, out var value, out _) || value is null)
            return null;
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int? TryGetCollectionCount(object value) {
        if (value is ICollection collection)
            return collection.Count;
        return TryReadProperty(value, "Count", out var count, out _) && count is int typedCount
            ? typedCount
            : null;
    }

    private static bool TryReadEnumerableProperty(
        object target,
        string propertyName,
        ControlSemanticSnapshot semantic,
        out IEnumerable enumerable) {
        enumerable = Array.Empty<object>();
        if (!TryReadProperty(target, propertyName, out var value, out var error)) {
            if (!string.IsNullOrEmpty(error))
                semantic.Errors[propertyName] = error!;
            return false;
        }

        if (value is not IEnumerable typed)
            return false;
        enumerable = typed;
        return true;
    }

    private static ISet<int> TryReadSelectedIndexes(object target) {
        var result = new HashSet<int>();
        if (TryReadProperty(target, "SelectedIndex", out var selectedIndex, out _) && selectedIndex is int single)
            result.Add(single);
        if (TryReadProperty(target, "SelectedIndexs", out var selectedIndexes, out _) && selectedIndexes is IEnumerable enumerable) {
            foreach (var value in enumerable) {
                if (value is int intValue)
                    result.Add(intValue);
            }
        }

        return result;
    }

    private static IEnumerable<object?> EnumerateRows(object? dataSource) {
        if (dataSource is null)
            yield break;
        if (dataSource is BindingSource bindingSource) {
            foreach (var row in EnumerateRows(bindingSource.DataSource))
                yield return row;
            yield break;
        }
        if (dataSource is IDictionary dictionary) {
            foreach (DictionaryEntry entry in dictionary)
                yield return entry;
            yield break;
        }
        if (dataSource is IEnumerable enumerable && dataSource is not string) {
            foreach (var row in enumerable)
                yield return row;
            yield break;
        }

        yield return dataSource;
    }

    private static object? ReadRowValue(object? row, string key) {
        if (row is null || string.IsNullOrWhiteSpace(key))
            return null;
        if (row is DictionaryEntry dictionaryEntry) {
            if (string.Equals(Convert.ToString(dictionaryEntry.Key, System.Globalization.CultureInfo.InvariantCulture), key, StringComparison.Ordinal))
                return dictionaryEntry.Value;
            row = dictionaryEntry.Value;
        }
        if (row is null)
            return null;
        if (row is IDictionary dictionary && dictionary.Contains(key))
            return dictionary[key];

        var properties = TypeDescriptor.GetProperties(row);
        var descriptor = properties.Find(key, ignoreCase: false) ?? properties.Find(key, ignoreCase: true);
        try {
            return descriptor?.GetValue(row);
        }
        catch {
            return null;
        }
    }

    private static void AddState(
        ControlSemanticSnapshot semantic,
        ControlProviderContext context,
        string name,
        object? value) =>
        semantic.State[name] = context.ToJsonValue(SanitizeValue(value));

    private static void AddMetadata(
        ControlSemanticSnapshot semantic,
        ControlProviderContext context,
        string name,
        object? value) =>
        semantic.Metadata[name] = context.ToJsonValue(SanitizeValue(value));

    private static string GetRuntimeType(Control control) =>
        control.GetType().FullName ?? control.GetType().Name;

    private static ControlProfile ResolveProfile(Type type) {
        for (var current = type; current is not null; current = current.BaseType) {
            var fullName = current.FullName;
            if (fullName is not null && Profiles.TryGetValue(fullName, out var profile))
                return profile;
        }

        return new ControlProfile("antdui-control", [], [], []);
    }

    private static bool IsAntdUIControl(Type type) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (current.FullName?.StartsWith(NamespacePrefix, StringComparison.Ordinal) == true)
                return true;
        }

        return false;
    }

    private static T TryGet<T>(Func<T> callback, T fallback) {
        try {
            return callback();
        }
        catch {
            return fallback;
        }
    }

    private enum ReadTarget {
        State,
        Property
    }

    private sealed class Read {
        private Read(string propertyName, string key, ReadTarget target) {
            PropertyName = propertyName;
            Key = key;
            Target = target;
        }

        public string PropertyName { get; }

        public string Key { get; }

        public ReadTarget Target { get; }

        public static Read State(string propertyName, string key) =>
            new(propertyName, key, ReadTarget.State);

        public static Read Property(string propertyName, string key) =>
            new(propertyName, key, ReadTarget.Property);
    }

    private sealed class ControlProfile {
        public ControlProfile(
            string semanticType,
            IReadOnlyList<string> interactionHints,
            IReadOnlyList<Read> stateReads,
            IReadOnlyList<Read> propertyReads,
            bool includeItems = false) {
            SemanticType = semanticType;
            InteractionHints = interactionHints;
            StateReads = stateReads;
            PropertyReads = propertyReads;
            IncludeItems = includeItems;
        }

        public string SemanticType { get; }

        public IReadOnlyList<string> InteractionHints { get; }

        public IReadOnlyList<Read> StateReads { get; }

        public IReadOnlyList<Read> PropertyReads { get; }

        public bool IncludeItems { get; }

        public IEnumerable<Read> Reads => StateReads.Concat(PropertyReads);
    }

    private sealed class CollectionPage {
        public List<IndexedItem> Items { get; } = new();

        public bool HasMore { get; set; }

        public bool OffsetLimited { get; set; }
    }

    private sealed class IndexedItem {
        public IndexedItem(int index, object? value) {
            Index = index;
            Value = value;
        }

        public int Index { get; }

        public object? Value { get; }
    }
}