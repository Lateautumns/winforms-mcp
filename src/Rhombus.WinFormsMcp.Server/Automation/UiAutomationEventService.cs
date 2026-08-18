using System.Diagnostics;

using FlaUI.Core.AutomationElements;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Raises supported UIA actions and observes UIA state changes.
/// </summary>
internal sealed class UiAutomationEventService {
    private readonly AutomationRuntimeContext _context;

    public UiAutomationEventService(AutomationRuntimeContext context) {
        _context = context;
    }

    public string RaiseEvent(AutomationElement element, string eventName) {
        switch (eventName.ToLowerInvariant()) {
            case "invoke":
                var invokePattern = element.Patterns.Invoke.PatternOrDefault
                    ?? throw new InvalidOperationException("Element does not support InvokePattern");
                invokePattern.Invoke();
                return "Invoked";
            case "toggle":
                var togglePattern = element.Patterns.Toggle.PatternOrDefault
                    ?? throw new InvalidOperationException("Element does not support TogglePattern");
                togglePattern.Toggle();
                return $"Toggled to {togglePattern.ToggleState.ValueOrDefault}";
            case "expand":
                var expandPattern = element.Patterns.ExpandCollapse.PatternOrDefault
                    ?? throw new InvalidOperationException("Element does not support ExpandCollapsePattern");
                expandPattern.Expand();
                return "Expanded";
            case "collapse":
                var collapsePattern = element.Patterns.ExpandCollapse.PatternOrDefault
                    ?? throw new InvalidOperationException("Element does not support ExpandCollapsePattern");
                collapsePattern.Collapse();
                return "Collapsed";
            case "select":
                var selectionPattern = element.Patterns.SelectionItem.PatternOrDefault
                    ?? throw new InvalidOperationException("Element does not support SelectionItemPattern");
                selectionPattern.Select();
                return "Selected";
            case "scroll_into_view":
                var scrollItemPattern = element.Patterns.ScrollItem.PatternOrDefault
                    ?? throw new InvalidOperationException("Element does not support ScrollItemPattern");
                scrollItemPattern.ScrollIntoView();
                return "Scrolled into view";
            default:
                throw new ArgumentException(
                    $"Unknown event '{eventName}'. Supported: invoke, toggle, expand, collapse, select, scroll_into_view");
        }
    }

    public async Task<(bool fired, string? eventDetails, long elapsedMs)> ListenForEventAsync(
        AutomationElement? element,
        string eventType,
        int timeoutMs,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var automation = _context.Automation;
        var stopwatch = Stopwatch.StartNew();

        switch (eventType.ToLowerInvariant()) {
            case "focus_changed": {
                    var initialFocused = automation.FocusedElement();
                    var initialName = initialFocused?.Name ?? "";
                    var initialId = initialFocused?.AutomationId ?? "";

                    while (stopwatch.ElapsedMilliseconds < timeoutMs) {
                        await Task.Delay(100, cancellationToken);
                        try {
                            var current = automation.FocusedElement();
                            if ((current?.Name ?? "") != initialName ||
                                (current?.AutomationId ?? "") != initialId) {
                                return (
                                    true,
                                    $"Focus changed to: {current?.Name ?? ""} ({current?.ControlType})",
                                    stopwatch.ElapsedMilliseconds);
                            }
                        }
                        catch {
                            // The previously focused element can disappear.
                        }
                    }
                    return (false, null, stopwatch.ElapsedMilliseconds);
                }
            case "structure_changed": {
                    var root = element ?? automation.GetDesktop();
                    int initialCount;
                    try {
                        initialCount = root.FindAllChildren().Length;
                    }
                    catch {
                        initialCount = 0;
                    }

                    while (stopwatch.ElapsedMilliseconds < timeoutMs) {
                        await Task.Delay(100, cancellationToken);
                        try {
                            var currentCount = root.FindAllChildren().Length;
                            if (currentCount != initialCount) {
                                return (
                                    true,
                                    $"Child count changed from {initialCount} to {currentCount}",
                                    stopwatch.ElapsedMilliseconds);
                            }
                        }
                        catch {
                            // The observed element can disappear.
                        }
                    }
                    return (false, null, stopwatch.ElapsedMilliseconds);
                }
            case "property_changed":
                return (
                    false,
                    "Use wait_for_condition for property change detection; it supports property-specific comparisons.",
                    0);
            default:
                throw new ArgumentException(
                    $"Unknown event type '{eventType}'. Supported: focus_changed, structure_changed, property_changed");
        }
    }
}