using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Server.Runtime;

/// <summary>
/// Bridges a managed snapshot to the legacy UIA cache without moving either
/// Control or AutomationElement across the process boundary.
/// </summary>
internal sealed class ManagedUiaCorrelationService {
    private readonly ISessionManager _session;

    public ManagedUiaCorrelationService(ISessionManager session) {
        _session = session;
    }

    public UiaCorrelation? TryCorrelate(ControlIdentity identity) {
        if (string.IsNullOrWhiteSpace(identity.AutomationId) &&
            string.IsNullOrWhiteSpace(identity.Name))
            return null;

        try {
            var automation = _session.GetAutomation();
            var element = !string.IsNullOrWhiteSpace(identity.AutomationId)
                ? automation.FindByAutomationId(identity.AutomationId!, timeoutMs: 500)
                : automation.FindByName(identity.Name, timeoutMs: 500);
            if (element is null)
                return null;

            var uiaId = _session.CacheElement(element);
            identity.UiaId = uiaId;
            return new UiaCorrelation {
                UiaId = uiaId,
                Method = !string.IsNullOrWhiteSpace(identity.AutomationId)
                    ? "automationId"
                    : "name",
                Confidence = !string.IsNullOrWhiteSpace(identity.AutomationId) ? 0.85 : 0.55
            };
        }
        catch {
            return null;
        }
    }
}
