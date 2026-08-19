using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

internal sealed partial class AntdUIProvider {
    private const int DefaultTableRowCount = 50;

    private static readonly HashSet<string> TableMemberAllowList = new(StringComparer.Ordinal) {
        "dataTmp", "SortData", "rows", "RowsCache", "List", "SHOW", "RD", "RECORD", "INDEX",
        "INDEX_REAL", "ENABLE", "Expand", "expand", "depth", "cells", "VALUE", "Value", "COLUMN"
    };

    private static TableScopeResult ResolveTableScope(Control control, string? requestedScope) {
        var requested = NormalizeTableScope(requestedScope, out var scopeWarning);
        var data = CreateDataScope(control);
        var visible = CreateVisibleScope(control);

        var result = requested switch {
            "data" => data,
            "visible" => visible ?? data.WithFallback(
                "data",
                "The AntdUI filtered/sorted row cache was unavailable; raw data rows were returned."),
            "rendered" => CreateRenderedScope(control) ?? (visible ?? data).WithFallback(
                visible is null ? "data" : "visible",
                "The AntdUI rendered row layout was unavailable; a non-rendered row scope was returned."),
            _ => data.WithFallback("data", scopeWarning ?? "The requested row scope was not supported.")
        };
        result.RequestedScope = requested;
        return result;
    }

    private static TableScopeResult CreateDataScope(Control control) {
        if (TryReadTableMember(control, "dataTmp", out var dataTmp, out _) && dataTmp is not null &&
            TryReadTableMember(dataTmp, "RowsCache", out var rows, out _) && rows is IEnumerable enumerable) {
            return new TableScopeResult("data", "data", enumerable, TryGetCollectionCount(rows));
        }

        if (TryReadProperty(control, "DataSource", out var dataSource, out _) && dataSource is not null) {
            var source = UnwrapBindingSource(dataSource);
            if (source is IEnumerable enumerableSource && source is not string)
                return new TableScopeResult("data", "data", enumerableSource, TryGetCollectionCount(source));

            return new TableScopeResult("data", "data", new object?[] { source }, 1);
        }

        return new TableScopeResult("data", "data", Array.Empty<object>(), 0);
    }

    private static TableScopeResult? CreateVisibleScope(Control control) {
        if (!TryReadTableMember(control, "dataTmp", out var dataTmp, out _) || dataTmp is null ||
            !TryReadTableMember(dataTmp, "rows", out var rows, out _) || rows is not IEnumerable enumerable)
            return null;

        var totalCount = TryGetCollectionCount(rows);
        var result = new TableScopeResult("visible", "visible", enumerable, totalCount);
        if (!TryReadTableMember(control, "SortData", out var sortData, out _) || sortData is null)
            return result;

        if (sortData is not int[] order || !IsValidRowOrder(order, totalCount)) {
            result.Diagnostics.Add("AntdUI SortData was present but could not be applied to the visible row cache.");
            return result;
        }

        result.Order = order;
        result.SortApplied = true;
        return result;
    }

    private static TableScopeResult? CreateRenderedScope(Control control) {
        if (!TryReadTableMember(control, "rows", out var rowList, out _) || rowList is null ||
            !TryReadTableMember(rowList, "List", out var rows, out _) || rows is not IEnumerable enumerable)
            return null;

        if (!CanReadRenderedVisibility(enumerable))
            return null;

        return new TableScopeResult("rendered", "rendered", enumerable, null) {
            IsRendered = true,
            Include = IsRenderedDataRow
        };
    }

    private static object? UnwrapBindingSource(object? value) {
        while (value is BindingSource bindingSource) {
            var dataSource = bindingSource.DataSource;
            if (ReferenceEquals(dataSource, value))
                break;
            value = dataSource;
        }

        return value;
    }

    private static bool CanReadRenderedVisibility(IEnumerable rows) {
        foreach (var row in rows) {
            if (row is null)
                continue;

            return TryReadTableMember(row, "SHOW", out var value, out _) && value is bool;
        }

        return false;
    }

    private static bool IsRenderedDataRow(object? row) {
        if (row is null ||
            !TryReadTableMember(row, "SHOW", out var show, out _) || show is not true)
            return false;

        if (TryReadProperty(row, "IsColumn", out var isColumn, out _) && isColumn is true)
            return false;
        if (TryReadProperty(row, "IsOther", out var isOther, out _) && isOther is true)
            return false;
        return true;
    }

    private static bool IsValidRowOrder(int[] order, int? rowCount) {
        if (!rowCount.HasValue || order.Length != rowCount.Value)
            return false;

        var seen = new HashSet<int>();
        foreach (var index in order) {
            if (index < 0 || index >= rowCount.Value || !seen.Add(index))
                return false;
        }

        return true;
    }

    private static string NormalizeTableScope(string? requestedScope, out string? warning) {
        warning = null;
        if (string.IsNullOrWhiteSpace(requestedScope))
            return "data";

        var normalized = requestedScope!.Trim().ToLowerInvariant();
        if (normalized is "data" or "visible" or "rendered")
            return normalized;

        warning = $"Unsupported AntdUI table row scope '{requestedScope}'.";
        return "invalid";
    }

    private static void AddTableScopeMetadata(
        ControlSemanticSnapshot semantic,
        ControlProviderContext context,
        TableScopeResult scope) {
        AddMetadata(semantic, context, "requestedRowScope", scope.RequestedScope);
        AddMetadata(semantic, context, "effectiveRowScope", scope.EffectiveScope);
        AddMetadata(semantic, context, "rowScope", scope.EffectiveScope);
        AddMetadata(semantic, context, "rowScopeAvailable", scope.FallbackReason is null);
        if (scope.TotalCount.HasValue)
            AddMetadata(semantic, context, "totalRowCount", scope.TotalCount.Value);
        else
            AddMetadata(semantic, context, "totalRowCountKnown", false);
        if (!string.IsNullOrWhiteSpace(scope.FallbackReason))
            AddMetadata(semantic, context, "rowScopeFallback", scope.FallbackReason);
        if (scope.SortApplied)
            AddMetadata(semantic, context, "sortOrderApplied", true);
        if (scope.Diagnostics.Count > 0)
            semantic.Errors["tableScope"] = string.Join(" ", scope.Diagnostics);
    }

    private static void AddTableStateMetadata(
        ControlSemanticSnapshot semantic,
        ControlProviderContext context,
        IReadOnlyList<TableColumnInfo> columns,
        TableScopeResult scope) {
        var sortableColumns = new List<string>();
        var activeSortColumns = new List<string>();
        var filteredColumns = new List<string>();
        var visibleColumns = 0;

        foreach (var column in columns) {
            if (column.Visible)
                visibleColumns++;
            if (column.Source is null)
                continue;

            if (TryReadBooleanProperty(column.Source, "SortOrder") is true)
                sortableColumns.Add(column.Key);
            var sortMode = TryReadStringProperty(column.Source, "SortMode");
            if (!string.IsNullOrWhiteSpace(sortMode) && !string.Equals(sortMode, "NONE", StringComparison.OrdinalIgnoreCase))
                activeSortColumns.Add(column.Key);
            if (TryReadBooleanProperty(column.Source, "HasFilter") is true)
                filteredColumns.Add(column.Key);
        }

        AddMetadata(semantic, context, "returnedColumnCount", columns.Count);
        AddMetadata(semantic, context, "returnedVisibleColumnCount", visibleColumns);
        semantic.Metadata["sortableColumns"] = SerializeSafe(sortableColumns);
        semantic.Metadata["activeSortColumns"] = SerializeSafe(activeSortColumns);
        semantic.Metadata["filterColumns"] = SerializeSafe(filteredColumns);
        AddMetadata(semantic, context, "hasActiveSort", activeSortColumns.Count > 0 || scope.SortApplied);
        AddMetadata(semantic, context, "hasActiveFilter", filteredColumns.Count > 0);
    }

    private static void AddColumnFilterMetadata(
        SemanticNodeSnapshot node,
        object column,
        ControlProviderContext context) {
        if (!TryReadProperty(column, "Filter", out var filter, out _) || filter is null)
            return;

        node.Properties["filterPresent"] = context.ToJsonValue(true);
        AddNodeState(node, filter, context, "Enabled", "filterEnabled");
        AddNodeProperty(node, filter, context, "Condition", "filterCondition");
        if (TryReadProperty(filter, "FilterValues", out var values, out _) && values is not null) {
            var count = TryGetCollectionCount(values);
            if (count.HasValue)
                node.Properties["filterValueCount"] = context.ToJsonValue(count.Value);
        }
    }

    private static TableRowPage ReadTableRowPage(TableScopeResult scope, int start, int limit) {
        var result = new TableRowPage();
        if (limit <= 0) {
            result.HasMore = scope.TotalCount.HasValue && start < scope.TotalCount.Value;
            return result;
        }

        if (scope.Source is IList indexed && scope.Include is null) {
            var count = scope.Order?.Length ?? indexed.Count;
            var end = Math.Min(count, SafeAdd(start, limit));
            for (var viewIndex = start; viewIndex < end; viewIndex++) {
                var sourceIndex = scope.Order is null ? viewIndex : scope.Order[viewIndex];
                result.Items.Add(CreateTableRowInfo(indexed[sourceIndex], viewIndex, sourceIndex, scope));
            }

            result.HasMore = end < count;
            return result;
        }

        if (start > MaximumNonIndexedPageOffset) {
            result.OffsetLimited = true;
            result.HasMore = scope.TotalCount.HasValue && start < scope.TotalCount.Value;
            return result;
        }

        var viewIndexFromEnumeration = 0;
        foreach (var sourceRow in scope.Source) {
            if (scope.Include is not null && !scope.Include(sourceRow))
                continue;

            var viewIndex = viewIndexFromEnumeration++;
            if (viewIndex < start)
                continue;
            if (result.Items.Count >= limit) {
                result.HasMore = true;
                break;
            }

            result.Items.Add(CreateTableRowInfo(sourceRow, viewIndex, viewIndex, scope));
        }

        if (!result.HasMore && scope.TotalCount.HasValue)
            result.HasMore = start + result.Items.Count < scope.TotalCount.Value;
        return result;
    }

    private static TableRowInfo CreateTableRowInfo(
        object? row,
        int viewIndex,
        int sourceIndex,
        TableScopeResult scope) {
        var template = scope.IsRendered ? row : null;
        var runtimeRow = row;
        if (template is not null && TryReadTableMember(template, "RD", out var renderedRow, out _) && renderedRow is not null)
            runtimeRow = renderedRow;

        var record = TryReadRowMember(runtimeRow, "record") ??
            TryReadTableMemberValue(template, "RECORD") ??
            runtimeRow;
        var rowSourceIndex = TryReadIntProperty(runtimeRow, "i") ??
            TryReadIntProperty(template, "INDEX_REAL") ??
            sourceIndex;
        var rowViewIndex = scope.IsRendered
            ? TryReadIntProperty(template, "INDEX") ?? viewIndex
            : viewIndex;
        var cells = TryReadRowMember(runtimeRow, "cells") as IDictionary;
        var depth = TryReadIntProperty(runtimeRow, "depth") ?? TryReadIntProperty(template, "Depth");
        var expanded = TryReadBooleanProperty(runtimeRow, "expand") ?? TryReadBooleanProperty(template, "Expand");
        var enabled = TryReadBooleanProperty(template, "ENABLE") ?? true;

        return new TableRowInfo(
            runtimeRow,
            template,
            record,
            cells,
            rowViewIndex,
            rowSourceIndex,
            scope.EffectiveScope,
            depth,
            expanded,
            enabled);
    }

    private static object? TryReadRowMember(object? row, string memberName) {
        if (row is null)
            return null;
        return TryReadProperty(row, memberName, out var value, out _) ? value : null;
    }

    private static object? TryReadTableMemberValue(object? target, string memberName) =>
        target is not null && TryReadTableMember(target, memberName, out var value, out _) ? value : null;

    private static SemanticNodeSnapshot BuildTableCellNode(
        TableRowInfo row,
        TableColumnInfo column,
        int columnIndex,
        ControlProviderContext context,
        ref int remainingNodes) {
        var value = ReadTableCellValue(row, column.Key, out var error);
        var text = ToSafeDisplayString(value);
        var node = new SemanticNodeSnapshot {
            Kind = "cell",
            Name = column.Key,
            Text = text,
            Value = text,
            Index = columnIndex,
            Properties = {
                ["columnKey"] = context.ToJsonValue(column.Key),
                ["columnTitle"] = context.ToJsonValue(column.Title),
                ["columnVisible"] = context.ToJsonValue(column.Visible)
            }
        };
        if (!string.IsNullOrWhiteSpace(error))
            node.Properties["error"] = context.ToJsonValue(error);
        if (value is not null)
            node.Properties["runtimeType"] = context.ToJsonValue(value.GetType().FullName);

        var buttons = ExtractCellButtons(row, column, Math.Max(1, Math.Min(remainingNodes + 1, 64)));
        node.ChildCount = buttons.Buttons.Count;
        foreach (var button in buttons.Buttons) {
            if (remainingNodes <= 0) {
                node.Truncated = true;
                break;
            }

            remainingNodes--;
            node.Children.Add(BuildCellButtonNode(button, node.Children.Count, context));
        }

        if (buttons.HasMore || node.Children.Count < node.ChildCount)
            node.Truncated = true;
        return node;
    }

    private static object? ReadTableCellValue(TableRowInfo row, string key, out string? error) {
        error = null;
        if (TryReadRawCellValue(row.Cells, key, out var rawValue))
            return UnwrapTableCellValue(rawValue, row.Record, out error);

        try {
            return ReadRowValue(row.Record, key);
        }
        catch (Exception ex) {
            error = ex.Message;
            return null;
        }
    }

    private static bool TryReadRawCellValue(IDictionary? cells, string key, out object? value) {
        value = null;
        if (cells is null || string.IsNullOrWhiteSpace(key))
            return false;

        try {
            if (cells.Contains(key)) {
                value = cells[key];
                return true;
            }

            foreach (DictionaryEntry entry in cells) {
                if (!string.Equals(Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase))
                    continue;
                value = entry.Value;
                return true;
            }
        }
        catch {
            return false;
        }

        return false;
    }

    private static object? UnwrapTableCellValue(object? value, object? record, out string? error) {
        error = null;
        if (value is PropertyDescriptor descriptor) {
            try {
                return record is null ? null : descriptor.GetValue(record);
            }
            catch (Exception ex) {
                error = ex.Message;
                return null;
            }
        }

        if (value is not null && string.Equals(value.GetType().FullName, "AntdUI.AntItem", StringComparison.Ordinal)) {
            if (TryReadProperty(value, "value", out var itemValue, out var itemError))
                return itemValue;
            if (!string.IsNullOrWhiteSpace(itemError))
                error = itemError;
        }

        return value;
    }

    private static CellButtonResult ExtractCellButtons(TableRowInfo row, TableColumnInfo column, int maximumCount) {
        var result = new CellButtonResult();
        if (row.Template is not null && TryReadProperty(row.Template, "cells", out var cells, out _) && cells is IEnumerable renderedCells) {
            foreach (var cell in renderedCells) {
                if (!IsCellForColumn(cell, column))
                    continue;
                AddCellButtonsFromValue(TryReadPropertyValue(cell, "Value"), result, maximumCount);
                AddCellButtonsFromValue(TryReadPropertyValue(cell, "VALUE"), result, maximumCount);
            }
        }

        if (TryReadRawCellValue(row.Cells, column.Key, out var rawValue))
            AddCellButtonsFromValue(UnwrapTableCellValue(rawValue, row.Record, out _), result, maximumCount);
        return result;
    }

    private static bool IsCellForColumn(object? cell, TableColumnInfo column) {
        if (cell is null || !TryReadProperty(cell, "COLUMN", out var cellColumn, out _))
            return false;
        if (ReferenceEquals(cellColumn, column.Source))
            return true;
        return cellColumn is not null &&
            string.Equals(TryReadStringProperty(cellColumn, "Key"), column.Key, StringComparison.Ordinal);
    }

    private static object? TryReadPropertyValue(object? target, string propertyName) =>
        target is not null && TryReadProperty(target, propertyName, out var value, out _) ? value : null;

    private static void AddCellButtonsFromValue(object? value, CellButtonResult result, int maximumCount) {
        if (value is null)
            return;
        if (IsCellButton(value)) {
            AddCellButton(value, result, maximumCount);
            return;
        }
        if (value is string || value is not IEnumerable items)
            return;

        foreach (var item in items) {
            if (IsCellButton(item))
                AddCellButton(item!, result, maximumCount);
            if (result.HasMore)
                break;
        }
    }

    private static void AddCellButton(object button, CellButtonResult result, int maximumCount) {
        if (result.Buttons.Any(existing => ReferenceEquals(existing, button)))
            return;
        if (result.Buttons.Count >= maximumCount) {
            result.HasMore = true;
            return;
        }

        result.Buttons.Add(button);
    }

    private static bool IsCellButton(object? value) {
        for (var type = value?.GetType(); type is not null; type = type.BaseType) {
            if (string.Equals(type.FullName, "AntdUI.CellButton", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static SemanticNodeSnapshot BuildCellButtonNode(
        object button,
        int index,
        ControlProviderContext context) {
        var id = TryReadStringProperty(button, "Id");
        var node = new SemanticNodeSnapshot {
            Kind = "cell-button",
            Name = id,
            Text = TryReadStringProperty(button, "Text"),
            Value = id,
            Index = index
        };
        AddRuntimeType(node, button, context);
        AddNodeState(node, button, context, "Enabled", "enabled");
        AddNodeState(node, button, context, "Loading", "loading");
        AddNodeProperty(node, button, context, "Id", "id");
        AddNodeProperty(node, button, context, "Tooltip", "tooltip");
        AddNodeProperty(node, button, context, "Type", "type");
        AddNodeProperty(node, button, context, "Shape", "shape");
        AddNodeProperty(node, button, context, "Radius", "radius");
        AddNodeProperty(node, button, context, "IconSvg", "iconSvg");
        AddNodeProperty(node, button, context, "Ghost", "ghost");
        AddNodeProperty(node, button, context, "LoadingSvg", "loadingSvg");
        return node;
    }

    private static bool TryReadTableMember(
        object target,
        string memberName,
        out object? value,
        out string? error) {
        value = null;
        error = null;
        if (!TableMemberAllowList.Contains(memberName)) {
            error = $"Table member '{memberName}' is not allowlisted.";
            return false;
        }

        for (var type = target.GetType(); type is not null; type = type.BaseType) {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var property = type.GetProperty(memberName, Flags);
            if (property is not null && property.GetIndexParameters().Length == 0) {
                try {
                    value = property.GetValue(target);
                    return true;
                }
                catch (Exception ex) {
                    error = ex.Message;
                    return false;
                }
            }

            var field = type.GetField(memberName, Flags);
            if (field is null)
                continue;
            try {
                value = field.GetValue(target);
                return true;
            }
            catch (Exception ex) {
                error = ex.Message;
                return false;
            }
        }

        return false;
    }

    private static int? TryReadIntProperty(object? target, string propertyName) {
        if (target is null || !TryReadProperty(target, propertyName, out var value, out _) || value is null)
            return null;
        return value switch {
            int integer => integer,
            _ when int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var parsed) => parsed,
            _ => null
        };
    }

    private static bool? TryReadBooleanProperty(object? target, string propertyName) {
        if (target is null || !TryReadProperty(target, propertyName, out var value, out _) || value is null)
            return null;
        return value is bool boolean ? boolean : null;
    }

    private static string? ToSafeDisplayString(object? value) {
        if (value is null)
            return null;
        try {
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch {
            return value.GetType().FullName;
        }
    }

    private static JsonElement SerializeSafe(object? value) => JsonSerializer.SerializeToElement(value);

    private sealed class TableScopeResult {
        public TableScopeResult(string requestedScope, string effectiveScope, IEnumerable source, int? totalCount) {
            RequestedScope = requestedScope;
            EffectiveScope = effectiveScope;
            Source = source;
            TotalCount = totalCount;
        }

        public string RequestedScope { get; internal set; }

        public string EffectiveScope { get; private set; }

        public IEnumerable Source { get; }

        public int? TotalCount { get; }

        public int[]? Order { get; set; }

        public bool SortApplied { get; set; }

        public bool IsRendered { get; set; }

        public Func<object?, bool>? Include { get; set; }

        public string? FallbackReason { get; private set; }

        public List<string> Diagnostics { get; } = new();

        public TableScopeResult WithFallback(string effectiveScope, string fallbackReason) {
            EffectiveScope = effectiveScope;
            FallbackReason = fallbackReason;
            return this;
        }
    }

    private sealed class TableColumnInfo {
        public TableColumnInfo(object? source, int index, string key, string? title, bool visible, SemanticNodeSnapshot node) {
            Source = source;
            Index = index;
            Key = key;
            Title = title;
            Visible = visible;
            Node = node;
        }

        public object? Source { get; }

        public int Index { get; }

        public string Key { get; }

        public string? Title { get; }

        public bool Visible { get; }

        public SemanticNodeSnapshot Node { get; }
    }

    private sealed class TableRowInfo {
        public TableRowInfo(
            object? source,
            object? template,
            object? record,
            IDictionary? cells,
            int viewIndex,
            int sourceIndex,
            string scope,
            int? depth,
            bool? expanded,
            bool enabled) {
            Source = source;
            Template = template;
            Record = record;
            Cells = cells;
            ViewIndex = viewIndex;
            SourceIndex = sourceIndex;
            Scope = scope;
            Depth = depth;
            Expanded = expanded;
            Enabled = enabled;
        }

        public object? Source { get; }

        public object? Template { get; }

        public object? Record { get; }

        public IDictionary? Cells { get; }

        public int ViewIndex { get; }

        public int SourceIndex { get; }

        public string Scope { get; }

        public int? Depth { get; }

        public bool? Expanded { get; }

        public bool Enabled { get; }
    }

    private sealed class TableRowPage {
        public List<TableRowInfo> Items { get; } = new();

        public bool HasMore { get; set; }

        public bool OffsetLimited { get; set; }
    }

    private sealed class CellButtonResult {
        public List<object> Buttons { get; } = new();

        public bool HasMore { get; set; }
    }
}