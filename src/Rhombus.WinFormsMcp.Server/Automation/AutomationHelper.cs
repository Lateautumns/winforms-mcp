using System.Diagnostics;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA2;

using Microsoft.Extensions.Logging;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Backward-compatible facade over the focused automation services.
/// </summary>
public sealed class AutomationHelper : IAutomationHelper {
    // Kept for binary/test compatibility with callers that inspect the legacy field.
    private UIA2Automation? _automation;

    private readonly AutomationRuntimeContext _context;
    private readonly HeadlessDesktopService _desktopService;
    private readonly ProcessService _processService;
    private readonly UiAutomationService _uiAutomationService;
    private readonly InputService _inputService;
    private readonly ScreenshotService _screenshotService;
    private readonly WindowService _windowService;
    private readonly ClipboardService _clipboardService;
    private readonly UiAutomationEventService _eventService;
    private bool _disposed;

    public AutomationHelper(bool headless = false, ILogger<AutomationHelper>? logger = null) {
        _context = new AutomationRuntimeContext();
        _automation = _context.Automation;
        try {
            _desktopService = new HeadlessDesktopService(headless, logger);
        }
        catch {
            _context.Dispose();
            _automation = null;
            throw;
        }
        _processService = new ProcessService(_desktopService, logger);
        _uiAutomationService = new UiAutomationService(_context, _desktopService);
        _inputService = new InputService(_desktopService);
        _screenshotService = new ScreenshotService(_context, _desktopService);
        _windowService = new WindowService(_context, _desktopService, _uiAutomationService);
        _clipboardService = new ClipboardService();
        _eventService = new UiAutomationEventService(_context);
    }

    public bool Headless => _desktopService.Headless;

    public Process LaunchApp(string path, string? arguments = null, string? workingDirectory = null) =>
        _processService.LaunchApp(path, arguments, workingDirectory);

    public Process AttachToProcess(int pid) => _processService.AttachToProcess(pid);

    public Process AttachToProcessByName(string name) => _processService.AttachToProcessByName(name);

    public AutomationElement? GetMainWindow(int pid) => _uiAutomationService.GetMainWindow(pid);

    public AutomationElement? GetElementFromHandle(IntPtr hwnd, int? pid = null) =>
        _uiAutomationService.GetElementFromHandle(hwnd, pid);

    public AutomationElement? FindByAutomationId(
        string automationId,
        AutomationElement? parent = null,
        int timeoutMs = 5000) =>
        _uiAutomationService.FindByAutomationId(automationId, parent, timeoutMs);

    public AutomationElement? FindByName(
        string name,
        AutomationElement? parent = null,
        int timeoutMs = 5000) =>
        _uiAutomationService.FindByName(name, parent, timeoutMs);

    public AutomationElement? FindByClassName(
        string className,
        AutomationElement? parent = null,
        int timeoutMs = 5000) =>
        _uiAutomationService.FindByClassName(className, parent, timeoutMs);

    public AutomationElement? FindByControlType(
        ControlType controlType,
        AutomationElement? parent = null,
        int timeoutMs = 5000) =>
        _uiAutomationService.FindByControlType(controlType, parent, timeoutMs);

    public AutomationElement[]? FindAll(
        ConditionBase condition,
        AutomationElement? parent = null,
        int timeoutMs = 5000) =>
        _uiAutomationService.FindAll(condition, parent, timeoutMs);

    public bool ElementExists(string automationId, AutomationElement? parent = null) =>
        _uiAutomationService.ElementExists(automationId, parent);

    public void Click(AutomationElement element, bool doubleClick = false) =>
        _inputService.Click(element, doubleClick);

    public void TypeText(AutomationElement element, string text, bool clearFirst = false) =>
        _inputService.TypeText(element, text, clearFirst);

    public void SetValue(AutomationElement element, string value) =>
        _inputService.SetValue(element, value);

    public object? GetProperty(AutomationElement element, string propertyName) =>
        _uiAutomationService.GetProperty(element, propertyName);

    public void TakeScreenshot(string outputPath, AutomationElement? element = null) =>
        _screenshotService.TakeScreenshot(outputPath, element);

    public void TakeScreenshot(string outputPath, AutomationElement? element, int? pid) =>
        _screenshotService.TakeScreenshot(outputPath, element, pid);

    public void DragDrop(AutomationElement source, AutomationElement target) =>
        _inputService.DragDrop(source, target);

    public void SendKeys(string keys, int? targetPid = null) =>
        _inputService.SendKeys(keys, targetPid);

    public void CloseApp(int pid, bool force = false) => _processService.CloseApp(pid, force);

    public Task<bool> WaitForElementAsync(
        string automationId,
        AutomationElement? parent = null,
        int timeoutMs = 10000) =>
        WaitForElementAsync(automationId, parent, timeoutMs, CancellationToken.None);

    public Task<bool> WaitForElementAsync(
        string automationId,
        AutomationElement? parent,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        _uiAutomationService.WaitForElementAsync(
            automationId,
            parent,
            timeoutMs,
            cancellationToken);

    public AutomationElement[]? GetAllChildren(AutomationElement element) =>
        _uiAutomationService.GetAllChildren(element);

    public Dictionary<string, object?> GetProcessStatus(int pid) =>
        _processService.GetProcessStatus(pid);

    public string SelectItem(AutomationElement element, string? value = null, int? index = null) =>
        _uiAutomationService.SelectItem(element, value, index);

    public void ClickMenuItem(string[] menuPath, int? pid = null) =>
        _uiAutomationService.ClickMenuItem(menuPath, pid);

    public T OnProcessDesktop<T>(int pid, Func<T> action) =>
        _desktopService.OnProcessDesktop(pid, action);

    public void OnProcessDesktop(int pid, Action action) =>
        _desktopService.OnProcessDesktop(pid, action);

    public List<Dictionary<string, object?>> GetElementTree(
        AutomationElement root,
        int depth = 3,
        int maxElements = 50,
        Func<AutomationElement, string>? cacheElement = null) =>
        _uiAutomationService.GetElementTree(root, depth, maxElements, cacheElement);

    public Task<(bool matched, string? actualValue, long elapsedMs)> WaitForConditionAsync(
        AutomationElement element,
        string propertyName,
        string expectedValue,
        string comparison = "equals",
        int timeoutMs = 10000) =>
        WaitForConditionAsync(
            element,
            propertyName,
            expectedValue,
            comparison,
            timeoutMs,
            CancellationToken.None);

    public Task<(bool matched, string? actualValue, long elapsedMs)> WaitForConditionAsync(
        AutomationElement element,
        string propertyName,
        string expectedValue,
        string comparison,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        _uiAutomationService.WaitForConditionAsync(
            element,
            propertyName,
            expectedValue,
            comparison,
            timeoutMs,
            cancellationToken);

    public (string previousState, string currentState) Toggle(
        AutomationElement element,
        string? desiredState = null) =>
        _uiAutomationService.Toggle(element, desiredState);

    public Dictionary<string, object> Scroll(
        AutomationElement element,
        string direction,
        int amount = 1,
        string scrollType = "line") =>
        _uiAutomationService.Scroll(element, direction, amount, scrollType);

    public Dictionary<string, object?> GetTableData(
        AutomationElement element,
        int startRow = 0,
        int rowCount = 50,
        int[]? columns = null) =>
        _uiAutomationService.GetTableData(element, startRow, rowCount, columns);

    public (string? previousValue, string? newValue) SetTableCell(
        AutomationElement element,
        int row,
        int column,
        string value) =>
        _uiAutomationService.SetTableCell(element, row, column, value);

    public Dictionary<string, object?> ManageWindow(
        int pid,
        string action,
        int? width = null,
        int? height = null,
        int? x = null,
        int? y = null) =>
        _windowService.ManageWindow(pid, action, width, height, x, y);

    public List<Dictionary<string, object?>> ListWindows(int pid) =>
        _windowService.ListWindows(pid);

    public AutomationElement? GetFocusedElement() => _uiAutomationService.GetFocusedElement();

    public string RaiseEvent(AutomationElement element, string eventName) =>
        _eventService.RaiseEvent(element, eventName);

    public Task<(bool fired, string? eventDetails, long elapsedMs)> ListenForEventAsync(
        AutomationElement? element,
        string eventType,
        int timeoutMs = 10000) =>
        ListenForEventAsync(element, eventType, timeoutMs, CancellationToken.None);

    public Task<(bool fired, string? eventDetails, long elapsedMs)> ListenForEventAsync(
        AutomationElement? element,
        string eventType,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        _eventService.ListenForEventAsync(element, eventType, timeoutMs, cancellationToken);

    public AutomationElement? OpenContextMenu(AutomationElement element) =>
        _windowService.OpenContextMenu(element);

    public string? GetClipboardText() => _clipboardService.GetText();

    public void SetClipboardText(string text) => _clipboardService.SetText(text);

    public string? GetTooltipText(AutomationElement element) =>
        _uiAutomationService.GetTooltipText(element);

    public AutomationElement[]? FindAllMatching(
        string? automationId = null,
        string? name = null,
        string? className = null,
        string? controlType = null,
        AutomationElement? parent = null,
        int timeoutMs = 5000) =>
        _uiAutomationService.FindAllMatching(
            automationId,
            name,
            className,
            controlType,
            parent,
            timeoutMs);

    internal string GetStderr(int pid) => _processService.GetStderr(pid);

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;

        _processService.Dispose();
        _context.Dispose();
        _automation = null;
        _desktopService.Dispose();
    }
}