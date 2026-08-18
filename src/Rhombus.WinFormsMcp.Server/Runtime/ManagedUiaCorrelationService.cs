using FlaUI.Core.AutomationElements;

using Rhombus.WinFormsMcp.RuntimeContracts;
using Rhombus.WinFormsMcp.Server.Automation;

namespace Rhombus.WinFormsMcp.Server.Runtime;

/// <summary>
/// Bridges a managed snapshot to the legacy UIA cache without moving either
/// Control or AutomationElement across the process boundary.
/// </summary>
internal sealed class ManagedUiaCorrelationService {
    private const int LookupTimeoutMs = 500;
    private const int TraversalMaxDepth = 8;
    private const int TraversalMaxNodes = 500;

    private readonly ISessionManager _session;

    public ManagedUiaCorrelationService(ISessionManager session) {
        _session = session;
    }

    public UiaCorrelation? TryCorrelate(ControlIdentity identity) {
        var targetHwnd = ParseHwnd(identity.Hwnd);
        if (string.IsNullOrWhiteSpace(identity.AutomationId) &&
            string.IsNullOrWhiteSpace(identity.Name) &&
            targetHwnd == IntPtr.Zero)
            return null;

        try {
            var automation = _session.GetAutomation();
            var mainWindow = identity.ProcessId > 0 ? automation.GetMainWindow(identity.ProcessId) : null;
            var candidate =
                FindManagedElement(automation, identity, mainWindow) ??
                FindByNativeWindowHandle(automation, identity, targetHwnd, mainWindow) ??
                FindManagedElement(automation, identity, null) ??
                FindByNativeWindowHandle(automation, identity, targetHwnd, null);
            if (candidate is null)
                return null;

            var uiaId = _session.CacheElement(candidate.Element);
            identity.UiaId = uiaId;
            return new UiaCorrelation {
                UiaId = uiaId,
                Method = candidate.Method,
                Confidence = candidate.Confidence
            };
        }
        catch {
            return null;
        }
    }

    private static CorrelationCandidate? FindManagedElement(
        IAutomationHelper automation,
        ControlIdentity identity,
        AutomationElement? parent) {
        if (!string.IsNullOrWhiteSpace(identity.AutomationId)) {
            var byAutomationId = TryFind(() => automation.FindByAutomationId(
                identity.AutomationId!,
                parent,
                LookupTimeoutMs));
            if (byAutomationId is not null && MatchesProcess(byAutomationId, identity.ProcessId))
                return new CorrelationCandidate(byAutomationId, "automationId", parent is null ? 0.75 : 0.85);
        }

        if (!string.IsNullOrWhiteSpace(identity.Name)) {
            var byName = TryFind(() => automation.FindByName(identity.Name, parent, LookupTimeoutMs));
            if (byName is not null && MatchesProcess(byName, identity.ProcessId))
                return new CorrelationCandidate(byName, "name", parent is null ? 0.45 : 0.55);
        }

        return null;
    }

    private static CorrelationCandidate? FindByNativeWindowHandle(
        IAutomationHelper automation,
        ControlIdentity identity,
        IntPtr targetHwnd,
        AutomationElement? parent) {
        if (targetHwnd == IntPtr.Zero)
            return null;

        var fromHandle = TryFind(() => automation.GetElementFromHandle(
            targetHwnd,
            identity.ProcessId > 0 ? identity.ProcessId : null));
        if (fromHandle is not null && MatchesProcess(fromHandle, identity.ProcessId))
            return new CorrelationCandidate(fromHandle, "nativeWindowHandle", 0.95);

        if (parent is null)
            return null;

        var traversed = FindByNativeWindowHandleTraversal(automation, parent, targetHwnd, identity.ProcessId);
        return traversed is null
            ? null
            : new CorrelationCandidate(traversed, "nativeWindowHandleTraversal", 0.9);
    }

    private static AutomationElement? FindByNativeWindowHandleTraversal(
        IAutomationHelper automation,
        AutomationElement root,
        IntPtr targetHwnd,
        int processId) {
        var queue = new Queue<(AutomationElement Element, int Depth)>();
        queue.Enqueue((root, 0));
        var visited = 0;

        while (queue.Count > 0 && visited < TraversalMaxNodes) {
            var (element, depth) = queue.Dequeue();
            visited++;

            if (HasNativeWindowHandle(element, targetHwnd) && MatchesProcess(element, processId))
                return element;

            if (depth >= TraversalMaxDepth)
                continue;

            AutomationElement[]? children;
            try {
                children = automation.GetAllChildren(element);
            }
            catch {
                continue;
            }

            if (children is null)
                continue;

            foreach (var child in children) {
                if (visited + queue.Count >= TraversalMaxNodes)
                    break;
                queue.Enqueue((child, depth + 1));
            }
        }

        return null;
    }

    private static bool HasNativeWindowHandle(AutomationElement element, IntPtr targetHwnd) {
        try {
            return element.Properties.NativeWindowHandle.ValueOrDefault == targetHwnd;
        }
        catch {
            return false;
        }
    }

    private static bool MatchesProcess(AutomationElement element, int processId) {
        if (processId <= 0)
            return true;

        try {
            var elementProcessId = element.Properties.ProcessId.ValueOrDefault;
            return elementProcessId <= 0 || elementProcessId == processId;
        }
        catch {
            return true;
        }
    }

    private static IntPtr ParseHwnd(string? hwnd) {
        if (string.IsNullOrWhiteSpace(hwnd))
            return IntPtr.Zero;

        var trimmed = hwnd.Trim();
        var style = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? System.Globalization.NumberStyles.HexNumber
            : System.Globalization.NumberStyles.Integer;
        var valueText = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..]
            : trimmed;
        return long.TryParse(valueText, style, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? new IntPtr(value)
            : IntPtr.Zero;
    }

    private static AutomationElement? TryFind(Func<AutomationElement?> callback) {
        try {
            return callback();
        }
        catch {
            return null;
        }
    }

    private sealed record CorrelationCandidate(
        AutomationElement Element,
        string Method,
        double Confidence);
}