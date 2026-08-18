using System.Runtime.InteropServices;
using System.Text;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection;

internal static class Win32WindowInspector {
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x0008_0000;
    private const uint GwChild = 5;
    private const uint GwHwndNext = 2;
    private const uint GwOwner = 4;

    public static List<WindowSnapshot> GetProcessWindows(int processId, int maxNodes) {
        var result = new List<WindowSnapshot>();
        var handles = new List<IntPtr>();
        EnumWindows((hwnd, _) => {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == processId)
                handles.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        var count = new Counter();
        var visited = new HashSet<IntPtr>();
        foreach (var hwnd in handles) {
            if (count.Value >= maxNodes)
                break;
            var snapshot = CreateSnapshot(hwnd, processId, parent: null, count, maxNodes, visited);
            if (snapshot is not null)
                result.Add(snapshot);
        }

        return result;
    }

    private static WindowSnapshot? CreateSnapshot(
        IntPtr hwnd,
        int processId,
        IntPtr? parent,
        Counter count,
        int maxNodes,
        HashSet<IntPtr> visited) {
        if (count.Value >= maxNodes || hwnd == IntPtr.Zero || !visited.Add(hwnd))
            return null;

        count.Value++;
        var bounds = new NativeRect();
        GetWindowRect(hwnd, out bounds);
        var owner = GetWindow(hwnd, GwOwner);
        var className = ReadClassName(hwnd);
        var title = ReadTitle(hwnd);
        var kind = Classify(hwnd, className, parent, owner);
        var snapshot = new WindowSnapshot {
            Hwnd = FormatHandle(hwnd),
            ProcessId = processId,
            ClassName = className,
            Title = title,
            Bounds = new RectSnapshot {
                X = bounds.Left,
                Y = bounds.Top,
                Width = Math.Max(0, bounds.Right - bounds.Left),
                Height = Math.Max(0, bounds.Bottom - bounds.Top)
            },
            Visible = IsWindowVisible(hwnd),
            Enabled = IsWindowEnabled(hwnd),
            Owner = owner == IntPtr.Zero ? null : FormatHandle(owner),
            Parent = parent is null || parent == IntPtr.Zero ? null : FormatHandle(parent.Value),
            Kind = kind
        };

        // EnumChildWindows returns all descendants, not only immediate
        // children. Walk the sibling chain so the serialized tree has no
        // duplicate descendants and parent links remain accurate.
        for (var child = GetWindow(hwnd, GwChild);
             child != IntPtr.Zero && count.Value < maxNodes;
             child = GetWindow(child, GwHwndNext)) {
            GetWindowThreadProcessId(child, out var childPid);
            if (childPid != processId)
                continue;
            var childSnapshot = CreateSnapshot(child, processId, hwnd, count, maxNodes, visited);
            if (childSnapshot is not null)
                snapshot.Children.Add(childSnapshot);
        }

        return snapshot;
    }

    private sealed class Counter {
        public int Value;
    }

    private static string Classify(IntPtr hwnd, string className, IntPtr? parent, IntPtr owner) {
        if (string.Equals(className, "#32770", StringComparison.OrdinalIgnoreCase))
            return "Dialog";
        if (parent is not null && parent != IntPtr.Zero)
            return "Child Window";
        if (owner != IntPtr.Zero)
            return "Owned Window";

        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        return (style & WsExLayered) != 0 ? "Layered Window" : "Main Window";
    }

    private static string ReadTitle(IntPtr hwnd) {
        var length = GetWindowTextLength(hwnd);
        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadClassName(IntPtr hwnd) {
        var builder = new StringBuilder(256);
        _ = GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    internal static string FormatHandle(IntPtr handle) => $"0x{handle.ToInt64():X}";

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint command);

    private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index) {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new IntPtr(GetWindowLong32(hwnd, index));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);
}