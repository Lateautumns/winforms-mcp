using System.Diagnostics;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Manages process windows, window enumeration, and context-menu discovery.
/// </summary>
internal sealed class WindowService {
    private readonly AutomationRuntimeContext _context;
    private readonly HeadlessDesktopService _desktopService;
    private readonly UiAutomationService _uiAutomationService;

    public WindowService(
        AutomationRuntimeContext context,
        HeadlessDesktopService desktopService,
        UiAutomationService uiAutomationService) {
        _context = context;
        _desktopService = desktopService;
        _uiAutomationService = uiAutomationService;
    }

    public Dictionary<string, object?> ManageWindow(
        int pid,
        string action,
        int? width = null,
        int? height = null,
        int? x = null,
        int? y = null) {
        _ = _context.Automation;
        var mainWindow = _uiAutomationService.GetMainWindow(pid)
            ?? throw new InvalidOperationException($"Could not find main window for process {pid}");
        var window = mainWindow.AsWindow();
        var windowPattern = mainWindow.Patterns.Window.PatternOrDefault;
        var transformPattern = mainWindow.Patterns.Transform.PatternOrDefault;

        switch (action.ToLowerInvariant()) {
            case "maximize":
                (windowPattern ?? throw new InvalidOperationException("Window does not support WindowPattern"))
                    .SetWindowVisualState(WindowVisualState.Maximized);
                break;
            case "minimize":
                (windowPattern ?? throw new InvalidOperationException("Window does not support WindowPattern"))
                    .SetWindowVisualState(WindowVisualState.Minimized);
                break;
            case "restore":
                (windowPattern ?? throw new InvalidOperationException("Window does not support WindowPattern"))
                    .SetWindowVisualState(WindowVisualState.Normal);
                break;
            case "resize":
                if (transformPattern == null || !transformPattern.CanResize.ValueOrDefault)
                    throw new InvalidOperationException("Window does not support resizing via TransformPattern");
                transformPattern.Resize(width ?? 800, height ?? 600);
                break;
            case "move":
                if (transformPattern == null || !transformPattern.CanMove.ValueOrDefault)
                    throw new InvalidOperationException("Window does not support moving via TransformPattern");
                transformPattern.Move(x ?? 0, y ?? 0);
                break;
            case "close":
                window.Close();
                break;
            default:
                throw new ArgumentException(
                    $"Invalid action '{action}'. Use maximize, minimize, restore, resize, move, or close.");
        }

        var rectangle = mainWindow.BoundingRectangle;
        return new Dictionary<string, object?> {
            ["windowState"] = windowPattern?.WindowVisualState.ValueOrDefault.ToString() ?? "unknown",
            ["boundingRectangle"] = ToRectangle(rectangle)
        };
    }

    public List<Dictionary<string, object?>> ListWindows(int pid) {
        var automation = _context.Automation;
        var results = new List<Dictionary<string, object?>>();
        void ReadWindows() {
            var condition = new PropertyCondition(automation.PropertyLibrary.Element.ProcessId, pid);
            foreach (var window in automation.GetDesktop().FindAllChildren(condition)) {
                results.Add(new Dictionary<string, object?> {
                    ["title"] = window.Name,
                    ["className"] = window.ClassName,
                    ["isVisible"] = !window.IsOffscreen,
                    ["controlType"] = window.ControlType.ToString(),
                    ["boundingRectangle"] = ToRectangle(window.BoundingRectangle)
                });
            }
        }

        _desktopService.OnProcessDesktop(pid, ReadWindows);
        return results;
    }

    public AutomationElement? OpenContextMenu(AutomationElement element) {
        var automation = _context.Automation;
        var pid = element.Properties.ProcessId.ValueOrDefault;

        if (_desktopService.IsOnHiddenDesktop(pid)) {
            var hwnd = (IntPtr)element.Properties.NativeWindowHandle.ValueOrDefault;
            if (hwnd == IntPtr.Zero)
                hwnd = _desktopService.GetOrFindWindowHandle(pid);
            if (hwnd != IntPtr.Zero) {
                NativeMethods.SendContextMenuMessage(hwnd);
                Thread.Sleep(300);
            }
        }
        else {
            element.RightClick();
            Thread.Sleep(300);
        }

        var condition = new PropertyCondition(
            automation.PropertyLibrary.Element.ControlType,
            ControlType.Menu);
        AutomationElement[]? menus = null;
        if (_desktopService.IsOnHiddenDesktop(pid)) {
            menus = _desktopService.OnProcessDesktop(
                pid,
                () => automation.GetDesktop().FindAllChildren(condition));
        }
        else {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < 2000) {
                menus = automation.GetDesktop().FindAllChildren(condition);
                if (menus.Length > 0)
                    break;
                Thread.Sleep(100);
            }
        }

        return menus is { Length: > 0 } ? menus[^1] : null;
    }

    private static Dictionary<string, int> ToRectangle(System.Drawing.Rectangle rectangle) => new() {
        ["x"] = rectangle.X,
        ["y"] = rectangle.Y,
        ["width"] = rectangle.Width,
        ["height"] = rectangle.Height
    };
}