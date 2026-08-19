using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Server.Runtime;

internal sealed class NamedPipeRuntimeBridgeClient : IRuntimeBridgeClient, IDisposable {
    private const int MaximumResponseBytes = 4 * 1_048_576;
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
        CancellationToken cancellationToken,
        ControlSemanticOptions? semanticOptions = null) =>
        SendAsync<ControlInspectionSnapshot>(
            processId,
            RuntimeBridgeProtocol.InspectControl,
            new { controlId, sections, includeProperties, semanticOptions },
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

    public Task<IReadOnlyList<WindowSnapshot>> GetWindowTreeAsync(
        int processId,
        int maxNodes,
        CancellationToken cancellationToken) =>
        GetWindowTreeAsync(processId, maxNodes, cancellationToken, 100);

    public async Task<IReadOnlyList<WindowSnapshot>> GetWindowTreeAsync(
        int processId,
        int maxNodes,
        CancellationToken cancellationToken,
        int maxItems) =>
        await SendAsync<List<WindowSnapshot>>(
            processId,
            RuntimeBridgeProtocol.GetWindowTree,
            new { maxNodes, maxItems },
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

    public Task<RuntimeDiagnosticsSnapshot> DetectDiagnosticsAsync(
        int processId,
        string? rootId,
        IReadOnlyCollection<string>? checks,
        int maxDepth,
        int maxNodes,
        int maxDiagnostics,
        CancellationToken cancellationToken) =>
        SendAsync<RuntimeDiagnosticsSnapshot>(
            processId,
            RuntimeBridgeProtocol.DetectDiagnostics,
            new { rootId, checks, maxDepth, maxNodes, maxDiagnostics },
            cancellationToken);

    public Task<RuntimeAccessibilitySnapshot> GetAccessibilityAsync(
        int processId,
        string? rootId,
        int maxDepth,
        int maxNodes,
        int maxDiagnostics,
        CancellationToken cancellationToken) =>
        SendAsync<RuntimeAccessibilitySnapshot>(
            processId,
            RuntimeBridgeProtocol.GetAccessibility,
            new { rootId, maxDepth, maxNodes, maxDiagnostics },
            cancellationToken);

    public Task<RuntimeEventTraceSnapshot> StartEventTraceAsync(
        int processId,
        string? rootId,
        IReadOnlyCollection<string>? events,
        int maxEvents,
        int durationMs,
        int maxNodes,
        CancellationToken cancellationToken) =>
        SendAsync<RuntimeEventTraceSnapshot>(
            processId,
            RuntimeBridgeProtocol.StartEventTrace,
            new { rootId, events, maxEvents, durationMs, maxNodes },
            cancellationToken);

    public Task<RuntimeEventTraceSnapshot> ReadEventTraceAsync(
        int processId,
        string traceId,
        long afterSequence,
        int maxEvents,
        CancellationToken cancellationToken) =>
        SendAsync<RuntimeEventTraceSnapshot>(
            processId,
            RuntimeBridgeProtocol.ReadEventTrace,
            new { traceId, afterSequence, maxEvents },
            cancellationToken);

    public Task<RuntimeEventTraceSnapshot> StopEventTraceAsync(
        int processId,
        string traceId,
        CancellationToken cancellationToken) =>
        SendAsync<RuntimeEventTraceSnapshot>(
            processId,
            RuntimeBridgeProtocol.StopEventTrace,
            new { traceId },
            cancellationToken);

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
            ValidateServerProcess(pipe, processId);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) {
                AutoFlush = true
            };

            string? bridgeInstanceId = null;
            if (command is not RuntimeBridgeProtocol.Hello and not RuntimeBridgeProtocol.GetStatus) {
                var helloRequest = CreateRequest(processId, RuntimeBridgeProtocol.Hello, new { });
                var helloResponse = await SendRequestAsync(pipe, writer, helloRequest, timeout.Token).ConfigureAwait(false);
                EnsureSuccess(helloResponse);
                var hello = DeserializeResult<BridgeHello>(helloResponse);
                if (hello.Process.ProcessId != processId)
                    throw new RuntimeBridgeException(
                        "bridge_process_mismatch",
                        $"RuntimeBridge identified process {hello.Process.ProcessId}, expected {processId}.",
                        false);
                bridgeInstanceId = hello.BridgeInstanceId;
            }

            var request = CreateRequest(processId, command, arguments, bridgeInstanceId);
            var response = await SendRequestAsync(pipe, writer, request, timeout.Token).ConfigureAwait(false);
            EnsureSuccess(response);

            return DeserializeResult<T>(response);
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

    private static RuntimeRequest CreateRequest(
        int processId,
        string command,
        object arguments,
        string? bridgeInstanceId = null) => new() {
            ProtocolVersion = RuntimeBridgeProtocol.Version,
            RequestId = Guid.NewGuid().ToString("N"),
            Command = command,
            Pid = processId,
            BridgeInstanceId = bridgeInstanceId,
            Arguments = JsonSerializer.SerializeToElement(arguments, SerializerOptions)
        };

    private static async Task<RuntimeResponse> SendRequestAsync(
        Stream stream,
        StreamWriter writer,
        RuntimeRequest request,
        CancellationToken cancellationToken) {
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, SerializerOptions)).ConfigureAwait(false);
        var responseLine = await ReadLineAsync(stream, MaximumResponseBytes, cancellationToken).ConfigureAwait(false)
            ?? throw new RuntimeBridgeException("bridge_disconnected", "RuntimeBridge disconnected before responding.");
        var response = JsonSerializer.Deserialize<RuntimeResponse>(responseLine, SerializerOptions)
            ?? throw new RuntimeBridgeException("invalid_response", "RuntimeBridge returned an empty response.", false);
        if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
            throw new RuntimeBridgeException("invalid_response", "RuntimeBridge response requestId did not match the request.", false);
        return response;
    }

    private static void EnsureSuccess(RuntimeResponse response) {
        if (response.ProtocolVersion != RuntimeBridgeProtocol.Version)
            throw new RuntimeBridgeException(
                "protocol_version_unsupported",
                $"RuntimeBridge returned protocol version {response.ProtocolVersion}; expected {RuntimeBridgeProtocol.Version}.",
                false);
        if (response.Success)
            return;

        var error = response.Error;
        throw new RuntimeBridgeException(
            error?.Code ?? "runtime_error",
            error?.Message ?? "RuntimeBridge returned an error.",
            error?.Retryable ?? true);
    }

    private static T DeserializeResult<T>(RuntimeResponse response) {
        if (response.Result.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new RuntimeBridgeException("invalid_response", "RuntimeBridge response did not contain a result.", false);
        return JsonSerializer.Deserialize<T>(response.Result.GetRawText(), SerializerOptions)
            ?? throw new RuntimeBridgeException("invalid_response", "RuntimeBridge result could not be decoded.", false);
    }

    private static async Task<string?> ReadLineAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken) {
        var limit = Math.Max(1, maxBytes);
        var bytes = new List<byte>(Math.Min(limit, 4096));
        var buffer = new byte[4096];
        while (true) {
            var count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (count == 0)
                return bytes.Count == 0 ? null : DecodeLine(bytes);
            for (var index = 0; index < count; index++) {
                if (buffer[index] == (byte)'\n')
                    return DecodeLine(bytes);
                if (bytes.Count >= limit)
                    throw new RuntimeBridgeException("response_too_large", "RuntimeBridge response exceeds the configured size limit.", false);
                bytes.Add(buffer[index]);
            }
        }
    }

    private static string DecodeLine(List<byte> bytes) {
        var count = bytes.Count;
        if (count > 0 && bytes[count - 1] == (byte)'\r')
            count--;
        return new UTF8Encoding(false, true).GetString(bytes.ToArray(), 0, count);
    }

    private static void ValidateServerProcess(NamedPipeClientStream pipe, int expectedProcessId) {
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var serverProcessId))
            throw new RuntimeBridgeException(
                "bridge_identity_unavailable",
                "The operating system did not expose the RuntimeBridge server process ID.",
                true);
        if (serverProcessId != (uint)expectedProcessId)
            throw new RuntimeBridgeException(
                "bridge_process_mismatch",
                $"Named pipe server process is {serverProcessId}, expected {expectedProcessId}.",
                false);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(IntPtr pipe, out uint serverProcessId);
}