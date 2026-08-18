using System.Collections.Concurrent;

using FlaUI.Core.AutomationElements;

using Microsoft.Extensions.Logging;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Owns hidden-desktop lifecycle and the process-to-desktop/window mappings.
/// </summary>
internal sealed class HeadlessDesktopService : IDisposable {
    internal const string DesktopName = "McpAutomation";

    private readonly ConcurrentDictionary<int, IntPtr> _processDesktops = new();
    private readonly ConcurrentDictionary<int, IntPtr> _processWindows = new();
    private IntPtr _hiddenDesktop;

    public HeadlessDesktopService(bool headless, ILogger<AutomationHelper>? logger) {
        Headless = headless;
        if (!headless)
            return;

        _hiddenDesktop = NativeMethods.CreateHiddenDesktop(DesktopName);
        if (_hiddenDesktop == IntPtr.Zero) {
            throw new InvalidOperationException(
                $"Failed to create hidden desktop '{DesktopName}'. " +
                "Headless mode requires the CreateDesktop Win32 API.");
        }

        logger?.LogInformation("Headless mode enabled, created hidden desktop: {Desktop}", DesktopName);
    }

    public bool Headless { get; }
    public IntPtr HiddenDesktop => _hiddenDesktop;

    public void TrackHiddenProcess(int pid) => _processDesktops[pid] = _hiddenDesktop;

    public void TrackDefaultDesktopProcess(int pid) => _processDesktops[pid] = IntPtr.Zero;

    public void RemoveProcess(int pid) {
        _processDesktops.TryRemove(pid, out _);
        _processWindows.TryRemove(pid, out _);
    }

    public IntPtr GetDesktopForProcess(int pid) =>
        _processDesktops.TryGetValue(pid, out var desktop) ? desktop : IntPtr.Zero;

    public IntPtr GetOrFindWindowHandle(int pid) {
        if (_processWindows.TryGetValue(pid, out var cached) && cached != IntPtr.Zero)
            return cached;

        var desktop = GetDesktopForProcess(pid);
        if (desktop == IntPtr.Zero)
            return IntPtr.Zero;

        var hwnd = NativeMethods.FindWindowOnDesktop(desktop, pid);
        if (hwnd != IntPtr.Zero)
            _processWindows[pid] = hwnd;
        return hwnd;
    }

    public bool IsOnHiddenDesktop(int pid) => GetDesktopForProcess(pid) != IntPtr.Zero;

    public bool IsOnHiddenDesktop(AutomationElement element) {
        try {
            var pid = element.Properties.ProcessId.ValueOrDefault;
            return pid > 0 && IsOnHiddenDesktop(pid);
        }
        catch {
            return false;
        }
    }

    public void EnsureInputAvailable(
        AutomationElement element,
        string operation,
        string? alternative = null) {
        if (!IsOnHiddenDesktop(element))
            return;

        var message =
            $"{operation} requires input simulation and is not available for headless processes " +
            "(the target element belongs to a process on the hidden desktop).";
        if (alternative != null)
            message += $" Use {alternative} instead.";
        throw new InvalidOperationException(message);
    }

    public T OnProcessDesktop<T>(int pid, Func<T> action) {
        var desktop = GetDesktopForProcess(pid);
        return desktop != IntPtr.Zero ? NativeMethods.WithDesktop(desktop, action) : action();
    }

    public void OnProcessDesktop(int pid, Action action) {
        var desktop = GetDesktopForProcess(pid);
        if (desktop != IntPtr.Zero)
            NativeMethods.WithDesktop(desktop, action);
        else
            action();
    }

    public void Dispose() {
        _processDesktops.Clear();
        _processWindows.Clear();

        if (_hiddenDesktop == IntPtr.Zero)
            return;

        NativeMethods.CloseHiddenDesktop(_hiddenDesktop);
        _hiddenDesktop = IntPtr.Zero;
    }
}