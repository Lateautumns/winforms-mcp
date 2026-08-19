using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Server.Runtime;

internal interface IRuntimeBridgeClient {
    Task<BridgeStatus> GetStatusAsync(int processId, CancellationToken cancellationToken);

    Task<ControlTreeSnapshot> GetControlTreeAsync(
        int processId,
        string? rootId,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken);

    Task<ControlInspectionSnapshot> InspectControlAsync(
        int processId,
        string controlId,
        IReadOnlyCollection<string>? sections,
        IReadOnlyCollection<string>? includeProperties,
        CancellationToken cancellationToken,
        ControlSemanticOptions? semanticOptions = null);

    Task<IReadOnlyList<ControlAncestorSnapshot>> GetAncestorsAsync(
        int processId,
        string controlId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WindowSnapshot>> GetWindowTreeAsync(
        int processId,
        int maxNodes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Optional bounded provider-item limit. The default preserves the
    /// original client contract for implementations that do not support
    /// provider window metadata yet.
    /// </summary>
    Task<IReadOnlyList<WindowSnapshot>> GetWindowTreeAsync(
        int processId,
        int maxNodes,
        CancellationToken cancellationToken,
        int maxItems) => GetWindowTreeAsync(processId, maxNodes, cancellationToken);

    Task<IReadOnlyList<ControlBindingSnapshot>> GetBindingsAsync(
        int processId,
        string controlId,
        CancellationToken cancellationToken);
}