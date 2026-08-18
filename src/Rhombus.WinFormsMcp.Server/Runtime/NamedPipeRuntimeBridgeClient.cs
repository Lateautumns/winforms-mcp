using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Server.Runtime;

internal sealed class NamedPipeRuntimeBridgeClient : IRuntimeBridgeClient, IDisposable {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };

    private readonly IOptions<McpServerOptions> _options;
    private readonly ILogger<NamedPipeRuntimeBridgeClient> _logger;
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _processGates = new();
    private bool _disposed;

    public NamedPipeRuntimeBridgeClient(
        IOptions<McpServerOptions> options,
        ILogger<NamedPipeRuntimeBridgeClient> logger) {
        _options = options;
        _logger = logger;
    }

    public async Task<BridgeStatus> GetStatusAsync(int processId, CancellationToken cancellationToken) {
        if (!_options.Value.RuntimeBridgeEnabled) {
            return new BridgeStatus {
                Available = false,
                Connected = false,
                ProtocolVersion = RuntimeBridgeProtocol.Version,
                PipeName = RuntimeBridgeProtocol.GetPipeName(processId),
                Error = "RuntimeBridge is disabled by configuration."
            };
        }

        try {
            return await SendAsync<BridgeStatus>(
                processId,
                RuntimeBridgeProtocol.GetStatus,
                new { },
                cancellationToken).ConfigureAwait(false);
        }
        catch (RuntimeBridgeException ex) {
            return new BridgeStatus {
                Available = false,
                Connected = false,
                ProtocolVersion = RuntimeBridgeProtocol.Version,
                PipeName = RuntimeBridgeProtocol.GetPipeName(processId),
                Error = ex.Message
            };
        }
    }

    public Task<ControlTreeSnapshot> GetControlTreeAsync(
        int processId,
        string? rootId,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken) =>
        SendAsync<ControlTreeSnapshot>(
            processId,
            RuntimeBridgeProtocol.GetControlTree,
            new { rootId, maxDepth, maxNodes },
            cancellationToken);

    public Task<ControlInspectionSnapshot> InspectControlAsync(
        int processId,
        string controlId,
        IReadOnlyCollection<string>? sections,
        IReadOnlyCollection<string>? includeProperties,
        CancellationToken cancellationToken) =>
        SendAsync<ControlInspectionSnapshot>(
            processId,
            RuntimeBridgeProtocol.InspectControl,
            new { controlId, sections, includeProperties },
            cancellationToken);

    public async Task<IReadOnlyList<ControlAncestorSnapshot>> GetAncestorsAsync(
        int processId,
        string controlId,
        CancellationToken cancellationToken) =>
        await SendAsync<List<ControlAncestorSnapshot>>(
            processId,
            RuntimeBridgeProtocol.GetAncestors,
            new { controlId },
            cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<WindowSnapshot>> GetWindowTreeAsync(
        int processId,
        int maxNodes,
        CancellationToken cancellationToken) =>
        await SendAsync<List<WindowSnapshot>>(
            processId,
            RuntimeBridgeProtocol.GetWindowTree,
            new { maxNodes },
            cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ControlBindingSnapshot>> GetBindingsAsync(
        int processId,
        string controlId,
        CancellationToken cancellationToken) =>
        await SendAsync<List<ControlBindingSnapshot>>(
            processId,
            RuntimeBridgeProtocol.GetBindings,
            new { controlId },
            cancellationToken).ConfigureAwait(false);

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var gate in _processGates.Values)
            gate.Dispose();
        _processGates.Clear();
    }

    private async Task<T> SendAsync<T>(
        int processId,
        string command,
        object arguments,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_options.Value.RuntimeBridgeEnabled)
            throw new RuntimeBridgeException("runtime_bridge_disabled", "RuntimeBridge is disabled by configuration.", false);
        if (processId <= 0)
            throw new RuntimeBridgeException("invalid_process", "A positive process ID is required.", false);

        var gate = _processGates.GetOrAdd(processId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.Value.RuntimeBridgeRequestTimeoutMs);
            using var pipe = new NamedPipeClientStream(
                ".",
                RuntimeBridgeProtocol.GetPipeName(processId),
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await ConnectAsync(pipe, _options.Value.RuntimeBridgeConnectTimeoutMs, timeout.Token).ConfigureAwait(false);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) {
                AutoFlush = true
            };

            var request = new RuntimeRequest {
                ProtocolVersion = RuntimeBridgeProtocol.Version,
                RequestId = Guid.NewGuid().ToString("N"),
                Command = command,
                Pid = processId,
                Arguments = JsonSerializer.SerializeToElement(arguments, SerializerOptions)
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, SerializerOptions)).ConfigureAwait(false);
            var responseLine = await ReadLineAsync(reader, timeout.Token).ConfigureAwait(false)
                ?? throw new RuntimeBridgeException("bridge_disconnected", "RuntimeBridge disconnected before responding.");
            var response = JsonSerializer.Deserialize<RuntimeResponse>(responseLine, SerializerOptions)
                ?? throw new RuntimeBridgeException("invalid_response", "RuntimeBridge returned an empty response.", false);

            if (response.ProtocolVersion != RuntimeBridgeProtocol.Version) {
                throw new RuntimeBridgeException(
                    "protocol_version_unsupported",
                    $"RuntimeBridge returned protocol version {response.ProtocolVersion}; expected {RuntimeBridgeProtocol.Version}.",
                    false);
            }
            if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal)) {
                throw new RuntimeBridgeException(
                    "invalid_response",
                    "RuntimeBridge response requestId did not match the request.",
                    false);
            }

            if (!response.Success) {
                var error = response.Error;
                throw new RuntimeBridgeException(
                    error?.Code ?? "runtime_error",
                    error?.Message ?? "RuntimeBridge returned an error.",
                    error?.Retryable ?? true);
            }

            if (response.Result.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                throw new RuntimeBridgeException("invalid_response", "RuntimeBridge response did not contain a result.", false);

            return JsonSerializer.Deserialize<T>(response.Result.GetRawText(), SerializerOptions)
                ?? throw new RuntimeBridgeException("invalid_response", "RuntimeBridge result could not be decoded.", false);
        }
        catch (RuntimeBridgeException) {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new RuntimeBridgeException("runtime_bridge_timeout", "RuntimeBridge request timed out.");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (JsonException ex) {
            throw new RuntimeBridgeException(
                "invalid_response",
                $"RuntimeBridge returned invalid JSON: {ex.Message}",
                false,
                ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException) {
            _logger.LogDebug(ex, "RuntimeBridge unavailable for process {ProcessId}", processId);
            throw new RuntimeBridgeException(
                "runtime_bridge_unavailable",
                $"RuntimeBridge for process {processId} is unavailable: {ex.Message}",
                true,
                ex);
        }
        finally {
            gate.Release();
        }
    }

    private static async Task ConnectAsync(
        NamedPipeClientStream pipe,
        int timeoutMs,
        CancellationToken cancellationToken) {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);
        try {
            // Use the cancellable BCL overload so a startup timeout does not
            // leave an abandoned connection occupying the single pipe server.
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new TimeoutException("Named pipe connection timed out.");
        }
    }

    private static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken) {
        return await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
    }
}