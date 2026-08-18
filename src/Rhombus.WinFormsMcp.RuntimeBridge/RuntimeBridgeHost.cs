using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

using Rhombus.WinFormsMcp.RuntimeBridge.Hosting;
using Rhombus.WinFormsMcp.RuntimeBridge.Inspection;
using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge;

/// <summary>
/// Owns the named-pipe listener and turns requests into UI-thread snapshots.
/// </summary>
public sealed class RuntimeBridgeHost : IDisposable {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };

    private readonly RuntimeBridgeOptions _options;
    private readonly UiThreadDispatcher _dispatcher;
    private readonly ManagedControlInspector _inspector;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _listenerTask;
    private bool _disposed;

    internal RuntimeBridgeHost(RuntimeBridgeOptions options, System.Windows.Forms.Control? invoker) {
        _options = options;
        _dispatcher = new UiThreadDispatcher(invoker);
        _inspector = new ManagedControlInspector(options);
    }

    public bool IsRunning => _listenerTask is { IsCompleted: false } && !_disposed;
    public string PipeName => _options.EffectivePipeName;

    internal void Start() {
        if (_listenerTask is not null)
            return;
        _listenerTask = Task.Run(ListenLoopAsync);
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        _shutdown.Cancel();
        _shutdown.Dispose();
    }

    private async Task ListenLoopAsync() {
        while (!_shutdown.IsCancellationRequested) {
            try {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await WaitForConnectionAsync(server, _shutdown.Token).ConfigureAwait(false);
                await HandleClientAsync(server, _shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) {
                break;
            }
            catch (Exception ex) {
                Trace(ex);
                if (!_shutdown.IsCancellationRequested)
                    await Task.Delay(100, _shutdown.Token).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken) {
        using var reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) {
            AutoFlush = true
        };

        while (!cancellationToken.IsCancellationRequested) {
            var readTask = reader.ReadLineAsync();
            var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completed = await Task.WhenAny(readTask, cancelTask).ConfigureAwait(false);
            if (completed != readTask)
                break;

            var line = await readTask.ConfigureAwait(false);
            if (line is null)
                break;
            if (line.Length > _options.MaxRequestBytes) {
                await WriteResponseAsync(writer, CreateError(string.Empty, "request_too_large", "Runtime request exceeds the configured size limit.", false))
                    .ConfigureAwait(false);
                break;
            }

            RuntimeResponse response;
            try {
                var request = JsonSerializer.Deserialize<RuntimeRequest>(line, SerializerOptions)
                    ?? throw new InvalidOperationException("Request payload was empty.");
                response = await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) {
                response = CreateError(string.Empty, GetErrorCode(ex), ex.Message, ex is IOException or TimeoutException, ex);
            }

            await WriteResponseAsync(writer, response).ConfigureAwait(false);
        }
    }

    private async Task<RuntimeResponse> ExecuteRequestAsync(
        RuntimeRequest request,
        CancellationToken cancellationToken) {
        var requestId = string.IsNullOrWhiteSpace(request.RequestId)
            ? Guid.NewGuid().ToString("N")
            : request.RequestId;

        if (request.ProtocolVersion != RuntimeBridgeProtocol.Version)
            return CreateError(requestId, "protocol_version_unsupported", $"Unsupported protocol version {request.ProtocolVersion}.", false);
        if (request.Pid != Process.GetCurrentProcess().Id)
            return CreateError(
                requestId,
                "process_mismatch",
                $"RuntimeBridge belongs to process {Process.GetCurrentProcess().Id}, not {request.Pid}.",
                false);

        try {
            object result = request.Command switch {
                RuntimeBridgeProtocol.Hello => await _dispatcher.InvokeAsync(_inspector.GetHello, cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.GetStatus => await GetStatusAsync(cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.GetControlTree => await _dispatcher.InvokeAsync(
                    () => _inspector.GetControlTree(
                        request.Pid,
                        GetString(request.Arguments, "rootId"),
                        GetInt(request.Arguments, "maxDepth", 4),
                        GetInt(request.Arguments, "maxNodes", 200)),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.InspectControl => await _dispatcher.InvokeAsync(
                    () => _inspector.InspectControl(
                        request.Pid,
                        RequireString(request.Arguments, "controlId"),
                        GetStringArray(request.Arguments, "sections"),
                        GetStringArray(request.Arguments, "includeProperties")),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.GetAncestors => await _dispatcher.InvokeAsync(
                    () => _inspector.GetAncestors(
                        request.Pid,
                        RequireString(request.Arguments, "controlId")),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.GetWindowTree => await _dispatcher.InvokeAsync(
                    () => _inspector.GetWindowTree(
                        request.Pid,
                        GetInt(request.Arguments, "maxNodes", 200)),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.GetBindings => await _dispatcher.InvokeAsync(
                    () => _inspector.GetBindings(
                        request.Pid,
                        RequireString(request.Arguments, "controlId")),
                    cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown runtime command '{request.Command}'.")
            };

            return new RuntimeResponse {
                RequestId = requestId,
                ProtocolVersion = RuntimeBridgeProtocol.Version,
                Success = true,
                Result = ToJsonElement(result)
            };
        }
        catch (Exception ex) {
            return CreateError(requestId, GetErrorCode(ex), ex.Message, ex is IOException or TimeoutException, ex);
        }
    }

    private async Task<BridgeStatus> GetStatusAsync(CancellationToken cancellationToken) {
        var hello = await _dispatcher.InvokeAsync(_inspector.GetHello, cancellationToken).ConfigureAwait(false);
        return new BridgeStatus {
            Available = true,
            Connected = true,
            ProtocolVersion = hello.ProtocolVersion,
            Process = hello.Process,
            Capabilities = hello.Capabilities,
            PipeName = PipeName
        };
    }

    private static async Task WaitForConnectionAsync(NamedPipeServerStream server, CancellationToken cancellationToken) {
        var waitTask = server.WaitForConnectionAsync();
        var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
        if (await Task.WhenAny(waitTask, cancelTask).ConfigureAwait(false) != waitTask)
            cancellationToken.ThrowIfCancellationRequested();
        await waitTask.ConfigureAwait(false);
    }

    private static async Task WriteResponseAsync(StreamWriter writer, RuntimeResponse response) {
        await writer.WriteLineAsync(JsonSerializer.Serialize(response, SerializerOptions)).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static RuntimeResponse CreateError(
        string requestId,
        string code,
        string message,
        bool retryable,
        Exception? exception = null) => new() {
            RequestId = requestId,
            ProtocolVersion = RuntimeBridgeProtocol.Version,
            Success = false,
            Error = new RuntimeError {
                Code = code,
                Message = message,
                Retryable = retryable,
                ExceptionType = exception?.GetType().Name
            }
        };

    private static JsonElement ToJsonElement(object value) {
        var json = JsonSerializer.Serialize(value, value.GetType(), SerializerOptions);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string? GetString(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object &&
        arguments.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string RequireString(JsonElement arguments, string name) =>
        GetString(arguments, name) ?? throw new ArgumentException($"'{name}' is required.");

    private static int GetInt(JsonElement arguments, string name, int defaultValue) =>
        arguments.ValueKind == JsonValueKind.Object &&
        arguments.TryGetProperty(name, out var value) &&
        value.TryGetInt32(out var result)
            ? result
            : defaultValue;

    private static string[]? GetStringArray(JsonElement arguments, string name) {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Array)
            return null;
        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
    }

    private static string GetErrorCode(Exception exception) => exception switch {
        ArgumentException => "invalid_argument",
        IOException => "bridge_io_error",
        TimeoutException => "timeout",
        InvalidOperationException => "invalid_operation",
        _ => "runtime_error"
    };

    private void Trace(Exception exception) {
        if (_options.Debug)
            Debug.WriteLine($"RuntimeBridge error: {exception}");
    }
}
