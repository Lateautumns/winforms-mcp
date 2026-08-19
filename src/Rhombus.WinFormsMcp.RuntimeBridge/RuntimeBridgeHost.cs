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
    private readonly object _lifecycleGate = new();
    private readonly string _bridgeInstanceId = Guid.NewGuid().ToString("N");
    private Task? _listenerTask;
    private Task? _disposeTask;
    private NamedPipeServerStream? _activeServer;
    private bool _disposed;

    internal RuntimeBridgeHost(RuntimeBridgeOptions options, System.Windows.Forms.Control? invoker) {
        _options = options;
        _dispatcher = new UiThreadDispatcher(invoker);
        _inspector = new ManagedControlInspector(
            options,
            postToUi: action => {
                var dispatch = _dispatcher.InvokeAsync(
                    () => {
                        action();
                        return true;
                    },
                    CancellationToken.None);
                _ = dispatch.ContinueWith(
                    task => Trace(task.Exception!),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            },
            bridgeInstanceId: _bridgeInstanceId);
    }

    public bool IsRunning {
        get {
            lock (_lifecycleGate)
                return _listenerTask is { IsCompleted: false } && !_disposed;
        }
    }

    public string PipeName => _options.EffectivePipeName;

    internal void Start() {
        lock (_lifecycleGate) {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RuntimeBridgeHost));
            if (_listenerTask is null)
                _listenerTask = Task.Run(ListenLoopAsync);
        }
    }

    public void Dispose() {
        StopAsync().GetAwaiter().GetResult();
    }

    public Task StopAsync() {
        Task disposeTask;
        lock (_lifecycleGate) {
            if (_disposeTask is null) {
                _disposed = true;
                _shutdown.Cancel();
                try {
                    _activeServer?.Dispose();
                }
                catch (Exception ex) {
                    Trace(ex);
                }

                _disposeTask = CompleteDisposeAsync(_listenerTask);
            }

            disposeTask = _disposeTask;
        }

        return disposeTask;
    }

    private async Task ListenLoopAsync() {
        var cancellationToken = _shutdown.Token;
        while (!cancellationToken.IsCancellationRequested) {
            NamedPipeServerStream? server = null;
            try {
                server = CreatePipeServer();

                lock (_lifecycleGate) {
                    if (_disposed || cancellationToken.IsCancellationRequested)
                        break;
                    _activeServer = server;
                }

                await WaitForConnectionAsync(server, cancellationToken).ConfigureAwait(false);
                await HandleClientAsync(server, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                break;
            }
            catch (Exception ex) {
                Trace(ex);
                if (!cancellationToken.IsCancellationRequested) {
                    try {
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                        break;
                    }
                }
            }
            finally {
                if (server is not null) {
                    lock (_lifecycleGate) {
                        if (ReferenceEquals(_activeServer, server))
                            _activeServer = null;
                    }

                    server.Dispose();
                }
            }
        }
    }

    private async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken) {
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) {
            AutoFlush = true
        };
        string? sessionInstanceId = null;

        while (!cancellationToken.IsCancellationRequested) {
            BoundedLine line;
            try {
                line = await ReadLineAsync(stream, _options.MaxRequestBytes, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                break;
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested) {
                break;
            }

            if (line.Line is null && !line.TooLarge)
                break;
            if (line.TooLarge) {
                try {
                    await WriteResponseAsync(
                        writer,
                        CreateError(
                            string.Empty,
                            "request_too_large",
                            "Runtime request exceeds the configured size limit.",
                            false)).ConfigureAwait(false);
                }
                catch (IOException) when (cancellationToken.IsCancellationRequested) {
                }

                break;
            }

            RuntimeResponse response;
            try {
                var request = JsonSerializer.Deserialize<RuntimeRequest>(line.Line!, SerializerOptions)
                    ?? throw new InvalidOperationException("Request payload was empty.");
                response = await ExecuteRequestAsync(request, sessionInstanceId, cancellationToken).ConfigureAwait(false);
                if (response.Success &&
                    string.Equals(request.Command, RuntimeBridgeProtocol.Hello, StringComparison.Ordinal))
                    sessionInstanceId = TryGetBridgeInstanceId(response.Result);
            }
            catch (Exception ex) {
                response = CreateError(string.Empty, GetErrorCode(ex), ex.Message, ex is IOException or TimeoutException, ex);
            }

            if (cancellationToken.IsCancellationRequested)
                break;

            try {
                await WriteResponseAsync(writer, response).ConfigureAwait(false);
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested) {
                break;
            }
        }
    }

    private async Task<RuntimeResponse> ExecuteRequestAsync(
        RuntimeRequest request,
        string? sessionInstanceId,
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
        var isHandshake = string.Equals(request.Command, RuntimeBridgeProtocol.Hello, StringComparison.Ordinal) ||
            string.Equals(request.Command, RuntimeBridgeProtocol.GetStatus, StringComparison.Ordinal);
        if (!isHandshake) {
            var requestHasInstanceId = !string.IsNullOrWhiteSpace(request.BridgeInstanceId);
            var instanceMatches = sessionInstanceId is not null
                ? string.Equals(request.BridgeInstanceId, sessionInstanceId, StringComparison.Ordinal)
                : !requestHasInstanceId || string.Equals(request.BridgeInstanceId, _bridgeInstanceId, StringComparison.Ordinal);
            if (!instanceMatches)
                return CreateError(
                    requestId,
                    "bridge_instance_mismatch",
                    "The request does not belong to the current RuntimeBridge instance. Call hello first.",
                    false);
        }

        try {
            object result = request.Command switch {
                RuntimeBridgeProtocol.Hello => await GetHelloAsync(cancellationToken).ConfigureAwait(false),
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
                        GetStringArray(request.Arguments, "includeProperties"),
                        GetSemanticOptions(request.Arguments)),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.GetAncestors => await _dispatcher.InvokeAsync(
                    () => _inspector.GetAncestors(
                        request.Pid,
                        RequireString(request.Arguments, "controlId")),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.GetWindowTree => await _dispatcher.InvokeAsync(
                    () => _inspector.GetWindowTree(
                        request.Pid,
                        GetInt(request.Arguments, "maxNodes", 200),
                        GetInt(request.Arguments, "maxItems", 100)),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.GetBindings => await _dispatcher.InvokeAsync(
                    () => _inspector.GetBindings(
                        request.Pid,
                        RequireString(request.Arguments, "controlId")),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.DetectDiagnostics => await _dispatcher.InvokeAsync(
                    () => _inspector.DetectDiagnostics(
                        request.Pid,
                        GetString(request.Arguments, "rootId"),
                        GetStringArray(request.Arguments, "checks"),
                        GetInt(request.Arguments, "maxDepth", 6),
                        GetInt(request.Arguments, "maxNodes", 500),
                        GetInt(request.Arguments, "maxDiagnostics", 200),
                        cancellationToken),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.GetAccessibility => await _dispatcher.InvokeAsync(
                    () => _inspector.GetAccessibility(
                        request.Pid,
                        GetString(request.Arguments, "rootId"),
                        GetInt(request.Arguments, "maxDepth", 6),
                        GetInt(request.Arguments, "maxNodes", 500),
                        GetInt(request.Arguments, "maxDiagnostics", 200),
                        cancellationToken),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.StartEventTrace => await _dispatcher.InvokeAsync(
                    () => _inspector.StartEventTrace(
                        request.Pid,
                        GetString(request.Arguments, "rootId"),
                        GetStringArray(request.Arguments, "events"),
                        GetInt(request.Arguments, "maxEvents", 200),
                        GetInt(request.Arguments, "durationMs", 60_000),
                        GetInt(request.Arguments, "maxNodes", 500),
                        cancellationToken),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.ReadEventTrace => await _dispatcher.InvokeAsync(
                    () => _inspector.ReadEventTrace(
                        request.Pid,
                        RequireString(request.Arguments, "traceId"),
                        GetInt64(request.Arguments, "afterSequence", 0),
                        GetInt(request.Arguments, "maxEvents", 200)),
                    cancellationToken).ConfigureAwait(false),
                RuntimeBridgeProtocol.StopEventTrace => await _dispatcher.InvokeAsync(
                    () => _inspector.StopEventTrace(
                        request.Pid,
                        RequireString(request.Arguments, "traceId")),
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
        var hello = await GetHelloAsync(cancellationToken).ConfigureAwait(false);
        return new BridgeStatus {
            Available = true,
            Connected = true,
            ProtocolVersion = hello.ProtocolVersion,
            Process = hello.Process,
            Capabilities = hello.Capabilities,
            PipeName = PipeName,
            BridgeInstanceId = _bridgeInstanceId
        };
    }

    private async Task<BridgeHello> GetHelloAsync(CancellationToken cancellationToken) {
        var hello = await _dispatcher.InvokeAsync(_inspector.GetHello, cancellationToken).ConfigureAwait(false);
        hello.BridgeInstanceId = _bridgeInstanceId;
        return hello;
    }

    private static string? TryGetBridgeInstanceId(JsonElement result) {
        try {
            return result.Deserialize<BridgeHello>(SerializerOptions)?.BridgeInstanceId;
        }
        catch (JsonException) {
            return null;
        }
    }

    private NamedPipeServerStream CreatePipeServer() {
#if NETFRAMEWORK
        return new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            NamedPipeSecurity.CreateCurrentUserOnly());
#else
        return new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
#endif
    }

    private static async Task WaitForConnectionAsync(NamedPipeServerStream server, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using var registration = cancellationToken.Register(
            static state => ((NamedPipeServerStream)state!).Dispose(),
            server);
        try {
            await server.WaitForConnectionAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static async Task<BoundedLine> ReadLineAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using var registration = cancellationToken.Register(
            static state => ((Stream)state!).Dispose(),
            stream);
        try {
            var limit = Math.Max(1, maxBytes);
            var bytes = new List<byte>(Math.Min(limit, 4096));
            var buffer = new byte[4096];
            while (true) {
                var count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                    return new BoundedLine(bytes.Count == 0 ? null : DecodeLine(bytes), false);

                for (var index = 0; index < count; index++) {
                    var value = buffer[index];
                    if (value == (byte)'\n')
                        return new BoundedLine(DecodeLine(bytes), false);
                    if (bytes.Count >= limit)
                        return new BoundedLine(null, true);
                    bytes.Add(value);
                }
            }
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
            return new BoundedLine(null, false);
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested) {
            return new BoundedLine(null, false);
        }
    }

    private static string DecodeLine(List<byte> bytes) {
        var count = bytes.Count;
        if (count > 0 && bytes[count - 1] == (byte)'\r')
            count--;
        return new UTF8Encoding(false, true).GetString(bytes.ToArray(), 0, count);
    }

    private async Task CompleteDisposeAsync(Task? listenerTask) {
        try {
            if (listenerTask is not null && listenerTask.Id != Task.CurrentId)
                await listenerTask.ConfigureAwait(false);
        }
        catch (Exception ex) {
            Trace(ex);
        }
        finally {
            try {
                // Event removal is pure managed delegate cleanup. Dispatching it
                // back to the UI thread would deadlock when synchronous Stop()
                // is itself called from that UI thread during application exit.
                _inspector.Dispose();
            }
            catch (Exception ex) {
                Trace(ex);
            }

            // The listener captured the token and must be fully stopped before
            // its source is released. This also makes concurrent Dispose calls
            // observe the same completed shutdown task.
            _shutdown.Dispose();
        }
    }

    private async Task WriteResponseAsync(StreamWriter writer, RuntimeResponse response) {
        var json = JsonSerializer.Serialize(response, SerializerOptions);
        if (Encoding.UTF8.GetByteCount(json) > Math.Max(1, _options.MaxResponseBytes)) {
            response = CreateError(
                response.RequestId,
                "response_too_large",
                "Runtime response exceeds the configured size limit.",
                true);
            json = JsonSerializer.Serialize(response, SerializerOptions);
        }

        await writer.WriteLineAsync(json).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private readonly struct BoundedLine {
        public BoundedLine(string? line, bool tooLarge) {
            Line = line;
            TooLarge = tooLarge;
        }

        public string? Line { get; }
        public bool TooLarge { get; }
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
            Result = JsonSerializer.SerializeToElement<object?>(null, SerializerOptions),
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

    private static long GetInt64(JsonElement arguments, string name, long defaultValue) =>
        arguments.ValueKind == JsonValueKind.Object &&
        arguments.TryGetProperty(name, out var value) &&
        value.TryGetInt64(out var result)
            ? result
            : defaultValue;

    private static ControlSemanticOptions? GetSemanticOptions(JsonElement arguments) {
        if (arguments.ValueKind != JsonValueKind.Object)
            return null;

        var source = arguments;
        if (arguments.TryGetProperty("semanticOptions", out var nested) && nested.ValueKind == JsonValueKind.Object)
            source = nested;

        var options = new ControlSemanticOptions {
            MaxDepth = GetNullableInt(source, "maxDepth"),
            MaxNodes = GetNullableInt(source, "maxNodes"),
            Start = GetNullableInt(source, "start"),
            Count = GetNullableInt(source, "count"),
            StartRow = GetNullableInt(source, "startRow"),
            RowCount = GetNullableInt(source, "rowCount"),
            RowScope = GetString(source, "rowScope")
        };

        return options.MaxDepth.HasValue ||
            options.MaxNodes.HasValue ||
            options.Start.HasValue ||
            options.Count.HasValue ||
            options.StartRow.HasValue ||
            options.RowCount.HasValue ||
            options.RowScope is not null
            ? options
            : null;
    }

    private static int? GetNullableInt(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object &&
        arguments.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : null;

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