using System.Drawing;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Performs UIA-backed and simulated mouse/keyboard input.
/// </summary>
internal sealed class InputService {
    private readonly HeadlessDesktopService _desktopService;

    public InputService(HeadlessDesktopService desktopService) {
        _desktopService = desktopService;
    }

    public void Click(AutomationElement element, bool doubleClick = false) {
        if (doubleClick) {
            _desktopService.EnsureInputAvailable(
                element,
                "Double-click",
                "single click_element (which uses UIA InvokePattern)");
            element.DoubleClick();
            return;
        }

        var invokePattern = element.Patterns.Invoke.PatternOrDefault;
        if (invokePattern != null) {
            invokePattern.Invoke();
            return;
        }

        var togglePattern = element.Patterns.Toggle.PatternOrDefault;
        if (togglePattern != null) {
            togglePattern.Toggle();
            return;
        }

        var expandPattern = element.Patterns.ExpandCollapse.PatternOrDefault;
        if (expandPattern != null) {
            if (expandPattern.ExpandCollapseState == ExpandCollapseState.Collapsed)
                expandPattern.Expand();
            else
                expandPattern.Collapse();
            return;
        }

        _desktopService.EnsureInputAvailable(
            element,
            "click_element (no UIA pattern available for this control)",
            "get_property to read state, or set_value to change it");
        element.Click();
    }

    public void TypeText(AutomationElement element, string text, bool clearFirst = false) {
        var valuePattern = element.Patterns.Value.PatternOrDefault;
        if (valuePattern != null && !valuePattern.IsReadOnly) {
            valuePattern.SetValue(clearFirst ? text : (valuePattern.Value ?? "") + text);
            return;
        }

        _desktopService.EnsureInputAvailable(
            element,
            "type_text (ValuePattern not available on this control)",
            "send_keys on a visible process, or set_value if the control supports ValuePattern");
        element.Focus();
        if (clearFirst) {
            System.Windows.Forms.SendKeys.SendWait("^a");
            Thread.Sleep(100);
        }
        System.Windows.Forms.SendKeys.SendWait(text);
    }

    public void SetValue(AutomationElement element, string value) {
        var valuePattern = element.Patterns.Value.PatternOrDefault;
        if (valuePattern != null && !valuePattern.IsReadOnly) {
            valuePattern.SetValue(value);
            return;
        }

        _desktopService.EnsureInputAvailable(
            element,
            "set_value (ValuePattern not available or read-only on this control)");
        element.Focus();
        System.Windows.Forms.SendKeys.SendWait("^a");
        Thread.Sleep(50);
        System.Windows.Forms.SendKeys.SendWait(value);
    }

    public void DragDrop(AutomationElement source, AutomationElement target) {
        _desktopService.EnsureInputAvailable(source, "drag_drop", "click_element and set_value sequences");
        _desktopService.EnsureInputAvailable(target, "drag_drop", "click_element and set_value sequences");

        var sourceBounds = source.BoundingRectangle;
        var targetBounds = target.BoundingRectangle;
        if (sourceBounds.Width == 0 || targetBounds.Width == 0)
            throw new InvalidOperationException("Source or target element has invalid bounding rectangle");

        var sourceCenter = new Point(
            (int)(sourceBounds.X + sourceBounds.Width / 2),
            (int)(sourceBounds.Y + sourceBounds.Height / 2));
        var targetCenter = new Point(
            (int)(targetBounds.X + targetBounds.Width / 2),
            (int)(targetBounds.Y + targetBounds.Height / 2));

        source.Focus();
        System.Windows.Forms.Cursor.Position = sourceCenter;
        Thread.Sleep(100);
        System.Windows.Forms.SendKeys.SendWait("{LDown}");
        System.Windows.Forms.Cursor.Position = targetCenter;
        Thread.Sleep(200);
        System.Windows.Forms.SendKeys.SendWait("{LUp}");
    }

    public void SendKeys(string keys, int? targetPid = null) {
        if (targetPid != null && _desktopService.IsOnHiddenDesktop(targetPid.Value)) {
            throw new InvalidOperationException(
                "send_keys requires input simulation and is not available for headless processes " +
                "(the target process is running on the hidden desktop). " +
                "Use type_text or set_value (which use UIA ValuePattern) for text input on headless processes.");
        }
        System.Windows.Forms.SendKeys.SendWait(keys);
    }
}