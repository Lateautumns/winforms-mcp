using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Server.Runtime;

internal interface IRuntimeBridgeClient {
    Task<BridgeStatus> GetStatusAsync(int processId, CancellationToken cancellationToken);

    Task<ControlTreeSnapshot> GetControlTreeAsync(
        int processId,
        string? rootId,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken,
        string? bridgeInstanceId = null);

    Task<ControlInspectionSnapshot> InspectControlAsync(
        int processId,
        string controlId,
        IReadOnlyCollection<string>? sections,
        IReadOnlyCollection<string>? includeProperties,
        CancellationToken cancellationToken,
        ControlSemanticOptions? semanticOptions = null,
        string? bridgeInstanceId = null);

    Task<IReadOnlyList<ControlAncestorSnapshot>> GetAncestorsAsync(
        int processId,
        string controlId,
        CancellationToken cancellationToken,
        string? bridgeInstanceId = null);

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
        int maxItems,
        string? bridgeInstanceId = null) => GetWindowTreeAsync(processId, maxNodes, cancellationToken);

    Task<IReadOnlyList<ControlBindingSnapshot>> GetBindingsAsync(
        int processId,
        string controlId,
        CancellationToken cancellationToken,
        string? bridgeInstanceId = null);

    Task<RuntimeDiagnosticsSnapshot> DetectDiagnosticsAsync(
        int processId,
        string? rootId,
        IReadOnlyCollection<string>? checks,
        int maxDepth,
        int maxNodes,
        int maxDiagnostics,
        CancellationToken cancellationToken,
        string? bridgeInstanceId = null);

    Task<RuntimeAccessibilitySnapshot> GetAccessibilityAsync(
        int processId,
        string? rootId,
        int maxDepth,
        int maxNodes,
        int maxDiagnostics,
        CancellationToken cancellationToken,
        string? bridgeInstanceId = null);

    Task<RuntimeEventTraceSnapshot> StartEventTraceAsync(
        int processId,
        string? rootId,
        IReadOnlyCollection<string>? events,
        int maxEvents,
        int durationMs,
        int maxNodes,
        CancellationToken cancellationToken,
        string? bridgeInstanceId = null);

    Task<RuntimeEventTraceSnapshot> ReadEventTraceAsync(
        int processId,
        string traceId,
        long afterSequence,
        int maxEvents,
        CancellationToken cancellationToken,
        string? bridgeInstanceId = null);

    Task<RuntimeEventTraceSnapshot> StopEventTraceAsync(
        int processId,
        string traceId,
        CancellationToken cancellationToken,
        string? bridgeInstanceId = null);
}