using System.Diagnostics;
using System.Text.Json;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Queries the UIA tree and implements UIA pattern-based inspection operations.
/// </summary>
internal sealed class UiAutomationService {
    private readonly AutomationRuntimeContext _context;
    private readonly HeadlessDesktopService _desktopService;

    public UiAutomationService(
        AutomationRuntimeContext context,
        HeadlessDesktopService desktopService) {
        _context = context;
        _desktopService = desktopService;
    }

    public AutomationElement? GetMainWindow(int pid) {
        var automation = _context.Automation;
        try {
            if (_desktopService.IsOnHiddenDesktop(pid)) {
                var hwnd = _desktopService.GetOrFindWindowHandle(pid);
                return hwnd == IntPtr.Zero
                    ? null
                    : _desktopService.OnProcessDesktop(pid, () => automation.FromHandle(hwnd));
            }

            var process = Process.GetProcessById(pid);
            return process.MainWindowHandle == IntPtr.Zero
                ? null
                : automation.FromHandle(process.MainWindowHandle);
        }
        catch {
            return null;
        }
    }

    public AutomationElement? GetElementFromHandle(IntPtr hwnd, int? pid = null) {
        if (hwnd == IntPtr.Zero)
            return null;

        var automation = _context.Automation;
        try {
            return pid is > 0 && _desktopService.IsOnHiddenDesktop(pid.Value)
                ? _desktopService.OnProcessDesktop(pid.Value, () => automation.FromHandle(hwnd))
                : automation.FromHandle(hwnd);
        }
        catch {
            return null;
        }
    }

    public AutomationElement? FindByAutomationId(
        string automationId,
        AutomationElement? parent = null,
        int timeoutMs = 5000) {
        var automation = _context.Automation;
        return FindElement(
            new PropertyCondition(automation.PropertyLibrary.Element.AutomationId, automationId),
            parent,
            timeoutMs);
    }

    public AutomationElement? FindByName(
        string name,
        AutomationElement? parent = null,
        int timeoutMs = 5000) {
        var automation = _context.Automation;
        return FindElement(
            new PropertyCondition(automation.PropertyLibrary.Element.Name, name),
            parent,
            timeoutMs);
    }

    public AutomationElement? FindByClassName(
        string className,
        AutomationElement? parent = null,
        int timeoutMs = 5000) {
        var automation = _context.Automation;
        return FindElement(
            new PropertyCondition(automation.PropertyLibrary.Element.ClassName, className),
            parent,
            timeoutMs);
    }

    public AutomationElement? FindByControlType(
        ControlType controlType,
        AutomationElement? parent = null,
        int timeoutMs = 5000) {
        var automation = _context.Automation;
        return FindElement(
            new PropertyCondition(automation.PropertyLibrary.Element.ControlType, controlType),
            parent,
            timeoutMs);
    }

    public AutomationElement[]? FindAll(
        ConditionBase condition,
        AutomationElement? parent = null,
        int timeoutMs = 5000) {
        var root = parent ?? _context.Automation.GetDesktop();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs) {
            try {
                var elements = root.FindAllChildren(condition);
                if (elements.Length > 0)
                    return elements;
            }
            catch {
                // UIA elements can become stale while polling.
            }
            Thread.Sleep(100);
        }
        return null;
    }

    public bool ElementExists(string automationId, AutomationElement? parent = null) =>
        FindByAutomationId(automationId, parent, 1000) != null;

    public object? GetProperty(AutomationElement element, string propertyName) =>
        propertyName.ToLowerInvariant() switch {
            "name" => element.Name,
            "automationid" => element.AutomationId,
            "classname" => element.ClassName,
            "controltype" => element.ControlType.ToString(),
            "isoffscreen" => element.IsOffscreen,
            "isenabled" => element.IsEnabled,
            "value" or "text" => GetValuePatternValue(element),
            "ischecked" or "togglestate" => GetTogglePatternState(element),
            "isselected" => GetSelectionItemPatternIsSelected(element),
            "selecteditem" => GetSelectionPatternSelectedItem(element),
            "items" => GetChildItemNames(element),
            "itemcount" => element.FindAllChildren().Length,
            "boundingrectangle" => GetBoundingRectangleJson(element),
            "isexpanded" => GetExpandCollapseState(element),
            "min" => GetRangeValueMin(element),
            "max" => GetRangeValueMax(element),
            "current" => GetRangeValueCurrent(element),
            _ => null
        };

    public async Task<bool> WaitForElementAsync(
        string automationId,
        AutomationElement? parent,
        int timeoutMs,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var automation = _context.Automation;
        var root = parent ?? automation.GetDesktop();
        var condition = new PropertyCondition(
            automation.PropertyLibrary.Element.AutomationId,
            automationId);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                if (root.FindFirstChild(condition) != null)
                    return true;
            }
            catch {
                // UIA elements can become stale while polling.
            }
            await Task.Delay(100, cancellationToken);
        }
        return false;
    }

    public AutomationElement[]? GetAllChildren(AutomationElement element) {
        try {
            return element.FindAllChildren();
        }
        catch {
            return null;
        }
    }

    public List<Dictionary<string, object?>> GetElementTree(
        AutomationElement root,
        int depth = 3,
        int maxElements = 50,
        Func<AutomationElement, string>? cacheElement = null) {
        var result = new List<Dictionary<string, object?>>();
        var elementCount = 0;
        BuildElementTree(root, depth, maxElements, cacheElement, result, ref elementCount);
        return result;
    }

    public async Task<(bool matched, string? actualValue, long elapsedMs)> WaitForConditionAsync(
        AutomationElement element,
        string propertyName,
        string expectedValue,
        string comparison,
        int timeoutMs,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs) {
            cancellationToken.ThrowIfCancellationRequested();
            var actual = GetProperty(element, propertyName)?.ToString();
            if (CompareValues(actual, expectedValue, comparison))
                return (true, actual, stopwatch.ElapsedMilliseconds);
            await Task.Delay(100, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var finalValue = GetProperty(element, propertyName)?.ToString();
        return (
            CompareValues(finalValue, expectedValue, comparison),
            finalValue,
            stopwatch.ElapsedMilliseconds);
    }

    public string SelectItem(AutomationElement element, string? value = null, int? index = null) {
        if (value == null && index == null)
            throw new ArgumentException("Either value or index must be provided");

        try {
            var expandPattern = element.Patterns.ExpandCollapse.PatternOrDefault;
            if (expandPattern != null) {
                expandPattern.Expand();
                Thread.Sleep(200);
            }

            var children = element.FindAllChildren();
            if (children.Length == 0)
                throw new InvalidOperationException("No items found in the selection control");

            AutomationElement targetItem;
            if (value != null) {
                targetItem = children.FirstOrDefault(child => {
                    try {
                        return string.Equals(child.Name, value, StringComparison.OrdinalIgnoreCase);
                    }
                    catch {
                        return false;
                    }
                }) ?? throw new InvalidOperationException(
                    $"Item with value '{value}' not found in the selection control");
            }
            else {
                if (index!.Value < 0 || index.Value >= children.Length) {
                    throw new ArgumentOutOfRangeException(
                        nameof(index),
                        $"Index {index.Value} is out of range. Control has {children.Length} items.");
                }
                targetItem = children[index.Value];
            }

            var selectionPattern = targetItem.Patterns.SelectionItem.PatternOrDefault;
            if (selectionPattern != null) {
                selectionPattern.Select();
            }
            else {
                var scrollPattern = targetItem.Patterns.ScrollItem.PatternOrDefault;
                if (scrollPattern != null) {
                    scrollPattern.ScrollIntoView();
                    Thread.Sleep(100);
                }
                targetItem.Click();
            }

            if (expandPattern != null) {
                try {
                    expandPattern.Collapse();
                }
                catch {
                    // Some controls close the popup automatically after selection.
                }
            }
            return targetItem.Name ?? "";
        }
        catch (ArgumentException) {
            throw;
        }
        catch (InvalidOperationException) {
            throw;
        }
        catch (Exception exception) {
            throw new InvalidOperationException($"Failed to select item: {exception.Message}", exception);
        }
    }

    public void ClickMenuItem(string[] menuPath, int? pid = null) {
        if (menuPath == null || menuPath.Length == 0)
            throw new ArgumentException("menuPath must contain at least one menu item name");

        var automation = _context.Automation;
        AutomationElement? currentParent = pid == null
            ? automation.GetDesktop()
            : GetMainWindow(pid.Value)
                ?? throw new InvalidOperationException($"Could not find main window for process {pid.Value}");

        for (var index = 0; index < menuPath.Length; index++) {
            var menuItemName = menuPath[index];
            var condition = new PropertyCondition(automation.PropertyLibrary.Element.Name, menuItemName);
            AutomationElement? menuItem = null;
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < 5000) {
                try {
                    menuItem = currentParent.FindFirstDescendant(condition);
                    if (menuItem != null)
                        break;
                }
                catch {
                    // The menu tree changes while submenus open.
                }
                Thread.Sleep(100);
            }

            if (menuItem == null) {
                throw new InvalidOperationException(
                    $"Menu item '{menuItemName}' not found at level {index} of path " +
                    $"[{string.Join(" > ", menuPath)}]");
            }

            if (index < menuPath.Length - 1) {
                var expandPattern = menuItem.Patterns.ExpandCollapse.PatternOrDefault;
                if (expandPattern != null)
                    expandPattern.Expand();
                else
                    menuItem.Click();
                Thread.Sleep(200);
                currentParent = menuItem;
            }
            else {
                var invokePattern = menuItem.Patterns.Invoke.PatternOrDefault;
                if (invokePattern != null)
                    invokePattern.Invoke();
                else
                    menuItem.Click();
            }
        }
    }

    public (string previousState, string currentState) Toggle(
        AutomationElement element,
        string? desiredState = null) {
        var pattern = element.Patterns.Toggle.PatternOrDefault
            ?? throw new InvalidOperationException(
                "Element does not support TogglePattern (not a checkbox, radio button, or toggle button)");
        var previousState = pattern.ToggleState.ValueOrDefault.ToString();

        if (desiredState == null) {
            pattern.Toggle();
        }
        else {
            var target = desiredState.ToLowerInvariant() switch {
                "on" => ToggleState.On,
                "off" => ToggleState.Off,
                "indeterminate" => ToggleState.Indeterminate,
                _ => throw new ArgumentException(
                    $"Invalid desiredState '{desiredState}'. Use 'on', 'off', or 'indeterminate'.")
            };
            for (var attempt = 0; attempt < 3 && pattern.ToggleState.ValueOrDefault != target; attempt++)
                pattern.Toggle();
        }

        return (previousState, pattern.ToggleState.ValueOrDefault.ToString());
    }

    public Dictionary<string, object> Scroll(
        AutomationElement element,
        string direction,
        int amount = 1,
        string scrollType = "line") {
        var pattern = element.Patterns.Scroll.PatternOrDefault
            ?? throw new InvalidOperationException(
                "Element does not support ScrollPattern (not a scrollable control)");
        var increment = scrollType.Equals("page", StringComparison.OrdinalIgnoreCase)
            ? ScrollAmount.LargeIncrement
            : ScrollAmount.SmallIncrement;
        var decrement = scrollType.Equals("page", StringComparison.OrdinalIgnoreCase)
            ? ScrollAmount.LargeDecrement
            : ScrollAmount.SmallDecrement;

        for (var index = 0; index < amount; index++) {
            switch (direction.ToLowerInvariant()) {
                case "down":
                    pattern.Scroll(ScrollAmount.NoAmount, increment);
                    break;
                case "up":
                    pattern.Scroll(ScrollAmount.NoAmount, decrement);
                    break;
                case "right":
                    pattern.Scroll(increment, ScrollAmount.NoAmount);
                    break;
                case "left":
                    pattern.Scroll(decrement, ScrollAmount.NoAmount);
                    break;
                default:
                    throw new ArgumentException(
                        $"Invalid direction '{direction}'. Use up, down, left, or right.");
            }
        }

        return new Dictionary<string, object> {
            ["horizontalPercent"] = pattern.HorizontalScrollPercent.ValueOrDefault,
            ["verticalPercent"] = pattern.VerticalScrollPercent.ValueOrDefault,
            ["horizontallyScrollable"] = pattern.HorizontallyScrollable.ValueOrDefault,
            ["verticallyScrollable"] = pattern.VerticallyScrollable.ValueOrDefault
        };
    }

    public Dictionary<string, object?> GetTableData(
        AutomationElement element,
        int startRow = 0,
        int rowCount = 50,
        int[]? columns = null) {
        try {
            var grid = element.AsDataGridView();
            var rows = grid.Rows;
            var headers = grid.Header?.Columns.Select(column => column.Text ?? "").ToList() ?? [];
            return BuildTableResult(
                rows.Length,
                headers,
                startRow,
                rowCount,
                columns,
                row => rows[row].Cells.Select(cell => cell.Value).ToArray());
        }
        catch {
            return GetTableDataViaGrid(element, startRow, rowCount, columns);
        }
    }

    public (string? previousValue, string? newValue) SetTableCell(
        AutomationElement element,
        int row,
        int column,
        string value) {
        try {
            var rows = element.AsDataGridView().Rows;
            ValidateCellIndex(rows.Length, row, "Row");
            var cells = rows[row].Cells;
            ValidateCellIndex(cells.Length, column, "Column");
            var cell = cells[column];
            var previousValue = cell.Value;
            var pattern = cell.Patterns.Value.PatternOrDefault;
            if (pattern != null && !pattern.IsReadOnly) {
                pattern.SetValue(value);
            }
            else {
                cell.Click();
                Thread.Sleep(200);
                cell.FindFirstChild()?.Patterns.Value.PatternOrDefault?.SetValue(value);
            }
            return (previousValue, value);
        }
        catch (ArgumentOutOfRangeException) {
            throw;
        }
        catch {
            return SetTableCellViaGrid(element, row, column, value);
        }
    }

    public string? GetTooltipText(AutomationElement element) {
        try {
            var helpText = element.Properties.HelpText.ValueOrDefault;
            if (!string.IsNullOrEmpty(helpText))
                return helpText;
        }
        catch {
            // HelpText is not exposed by every provider.
        }

        var legacyPattern = element.Patterns.LegacyIAccessible.PatternOrDefault;
        if (legacyPattern != null) {
            try {
                var description = legacyPattern.Description.ValueOrDefault;
                if (!string.IsNullOrEmpty(description))
                    return description;
            }
            catch {
                // Legacy accessibility is optional.
            }
        }

        try {
            element.Focus();
            Thread.Sleep(500);
            var automation = _context.Automation;
            var condition = new PropertyCondition(
                automation.PropertyLibrary.Element.ControlType,
                ControlType.ToolTip);
            return automation.GetDesktop().FindFirstChild(condition)?.Name;
        }
        catch {
            return null;
        }
    }

    public AutomationElement[]? FindAllMatching(
        string? automationId = null,
        string? name = null,
        string? className = null,
        string? controlType = null,
        AutomationElement? parent = null,
        int timeoutMs = 5000) {
        var automation = _context.Automation;
        ConditionBase condition;
        if (automationId != null)
            condition = new PropertyCondition(automation.PropertyLibrary.Element.AutomationId, automationId);
        else if (name != null)
            condition = new PropertyCondition(automation.PropertyLibrary.Element.Name, name);
        else if (className != null)
            condition = new PropertyCondition(automation.PropertyLibrary.Element.ClassName, className);
        else if (controlType != null)
            condition = new PropertyCondition(
                automation.PropertyLibrary.Element.ControlType,
                Enum.Parse<ControlType>(controlType, true));
        else
            throw new ArgumentException(
                "At least one search criterion (automationId, name, className, controlType) is required");

        return FindAll(condition, parent, timeoutMs);
    }

    public AutomationElement? GetFocusedElement() {
        try {
            return _context.Automation.FocusedElement();
        }
        catch {
            return null;
        }
    }

    private AutomationElement? FindElement(
        ConditionBase condition,
        AutomationElement? parent,
        int timeoutMs) {
        var root = parent ?? _context.Automation.GetDesktop();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs) {
            try {
                var element = root.FindFirstChild(condition);
                if (element != null)
                    return element;
            }
            catch {
                // UIA elements can become stale while polling.
            }
            Thread.Sleep(100);
        }
        return null;
    }

    private void BuildElementTree(
        AutomationElement parent,
        int remainingDepth,
        int maxElements,
        Func<AutomationElement, string>? cacheElement,
        List<Dictionary<string, object?>> target,
        ref int elementCount) {
        if (remainingDepth <= 0 || elementCount >= maxElements)
            return;
        var children = GetAllChildren(parent);
        if (children == null)
            return;

        foreach (var child in children) {
            if (elementCount >= maxElements)
                break;
            elementCount++;
            var node = new Dictionary<string, object?> {
                ["name"] = TryGetElementProperty(() => child.Name),
                ["controlType"] = TryGetElementProperty(() => child.ControlType.ToString()),
                ["automationId"] = TryGetElementProperty(() => child.AutomationId),
                ["isEnabled"] = TryGetElementProperty(() => child.IsEnabled),
                ["isOffscreen"] = TryGetElementProperty(() => child.IsOffscreen)
            };
            try {
                var rectangle = child.BoundingRectangle;
                node["boundingRectangle"] = new Dictionary<string, object> {
                    ["x"] = rectangle.X,
                    ["y"] = rectangle.Y,
                    ["width"] = rectangle.Width,
                    ["height"] = rectangle.Height
                };
            }
            catch {
                node["boundingRectangle"] = null;
            }
            if (cacheElement != null)
                node["elementId"] = cacheElement(child);

            var childNodes = new List<Dictionary<string, object?>>();
            if (remainingDepth > 1 && elementCount < maxElements) {
                BuildElementTree(
                    child,
                    remainingDepth - 1,
                    maxElements,
                    cacheElement,
                    childNodes,
                    ref elementCount);
            }
            node["children"] = childNodes;
            target.Add(node);
        }
    }

    private Dictionary<string, object?> GetTableDataViaGrid(
        AutomationElement element,
        int startRow,
        int rowCount,
        int[]? columns) {
        var grid = element.AsGrid();
        var rows = grid.Rows;
        var headers = grid.Header?.Columns.Select(column => column.Name ?? "").ToList() ?? [];
        return BuildTableResult(
            rows.Length,
            headers,
            startRow,
            rowCount,
            columns,
            row => rows[row].Cells.Select(cell => cell.Name ?? cell.Properties.Name.ValueOrDefault).ToArray());
    }

    private static Dictionary<string, object?> BuildTableResult(
        int totalRows,
        List<string> headers,
        int startRow,
        int rowCount,
        int[]? columns,
        Func<int, string?[]> readRow) {
        var rows = new List<Dictionary<string, object?>>();
        for (var rowIndex = startRow; rowIndex < Math.Min(startRow + rowCount, totalRows); rowIndex++) {
            var values = readRow(rowIndex);
            var selected = columns == null
                ? values.ToList()
                : columns.Select(column => column >= 0 && column < values.Length ? values[column] : null).ToList();
            rows.Add(new Dictionary<string, object?> {
                ["rowIndex"] = rowIndex,
                ["cells"] = selected
            });
        }

        var fallbackColumnCount = totalRows > 0 ? readRow(0).Length : 0;
        return new Dictionary<string, object?> {
            ["rowCount"] = totalRows,
            ["columnCount"] = headers.Count > 0 ? headers.Count : fallbackColumnCount,
            ["headers"] = headers,
            ["rows"] = rows
        };
    }

    private static (string? previousValue, string? newValue) SetTableCellViaGrid(
        AutomationElement element,
        int row,
        int column,
        string value) {
        var rows = element.AsGrid().Rows;
        ValidateCellIndex(rows.Length, row, "Row");
        var cells = rows[row].Cells;
        ValidateCellIndex(cells.Length, column, "Column");
        var cell = cells[column];
        var previousValue = cell.Name;
        var pattern = cell.Patterns.Value.PatternOrDefault;
        if (pattern != null && !pattern.IsReadOnly)
            pattern.SetValue(value);
        return (previousValue, value);
    }

    private static void ValidateCellIndex(int count, int index, string label) {
        if (index < 0 || index >= count) {
            throw new ArgumentOutOfRangeException(
                label.Equals("Row", StringComparison.Ordinal) ? "row" : "column",
                $"{label} {index} is out of range (0-{count - 1})");
        }
    }

    private static bool CompareValues(string? actual, string expected, string comparison) =>
        comparison.ToLowerInvariant() switch {
            "equals" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "contains" => actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true,
            "not_equals" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "greater_than" => double.TryParse(actual, out var left) &&
                              double.TryParse(expected, out var right) && left > right,
            "less_than" => double.TryParse(actual, out var left) &&
                           double.TryParse(expected, out var right) && left < right,
            _ => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
        };

    private static object? GetValuePatternValue(AutomationElement element) =>
        element.Patterns.Value.IsSupported
            ? element.Patterns.Value.Pattern.Value.ValueOrDefault
            : element.Name;

    private static object GetTogglePatternState(AutomationElement element) =>
        element.Patterns.Toggle.IsSupported
            ? element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault.ToString()
            : "TogglePattern not supported on this element";

    private static object GetSelectionItemPatternIsSelected(AutomationElement element) =>
        element.Patterns.SelectionItem.IsSupported
            ? element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault
            : "SelectionItemPattern not supported on this element";

    private static object? GetSelectionPatternSelectedItem(AutomationElement element) {
        if (!element.Patterns.Selection.IsSupported)
            return "SelectionPattern not supported on this element";
        var selection = element.Patterns.Selection.Pattern.Selection.ValueOrDefault;
        return selection is { Length: > 0 } ? selection[0].Name : null;
    }

    private static string GetChildItemNames(AutomationElement element) =>
        JsonSerializer.Serialize(element.FindAllChildren().Select(child => child.Name ?? "").ToArray());

    private static string GetBoundingRectangleJson(AutomationElement element) {
        var rectangle = element.BoundingRectangle;
        return JsonSerializer.Serialize(new {
            x = rectangle.X,
            y = rectangle.Y,
            width = rectangle.Width,
            height = rectangle.Height
        });
    }

    private static object GetExpandCollapseState(AutomationElement element) =>
        element.Patterns.ExpandCollapse.IsSupported
            ? element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault.ToString()
            : "ExpandCollapsePattern not supported on this element";

    private static object GetRangeValueMin(AutomationElement element) =>
        element.Patterns.RangeValue.IsSupported
            ? element.Patterns.RangeValue.Pattern.Minimum.ValueOrDefault
            : "RangeValuePattern not supported on this element";

    private static object GetRangeValueMax(AutomationElement element) =>
        element.Patterns.RangeValue.IsSupported
            ? element.Patterns.RangeValue.Pattern.Maximum.ValueOrDefault
            : "RangeValuePattern not supported on this element";

    private static object GetRangeValueCurrent(AutomationElement element) =>
        element.Patterns.RangeValue.IsSupported
            ? element.Patterns.RangeValue.Pattern.Value.ValueOrDefault
            : "RangeValuePattern not supported on this element";

    private static object? TryGetElementProperty(Func<object?> getter) {
        try {
            return getter();
        }
        catch {
            return null;
        }
    }
}