using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Rhombus.WinFormsMcp.Server.Automation.UiaWorker;

/// <summary>
/// Runs selected read-only UIA queries out of process so a blocked COM provider
/// can be terminated without wedging the MCP server.
/// </summary>
internal sealed class UiaWorkerProcess : IDisposable {
    private const int MaximumRequestBytes = 1_048_576;
    private const int MaximumDiagnosticCharacters = 8_192;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };

    private readonly McpServerOptions _options;
    private readonly ILogger<UiaWorkerProcess> _logger;
    private readonly string? _configuredWorkerPath;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _lifecycleGate = new();
    private readonly object _processGate = new();
    private readonly object _diagnosticGate = new();
    private readonly StringBuilder _stderr = new();
    private Process? _process;
    private StreamWriter? _stdin;
    private Stream? _stdout;
    private TaskCompletionSource<bool>? _disposeCompletion;
    private volatile bool _disposed;

    public UiaWorkerProcess(
        IOptions<McpServerOptions> options,
        ILogger<UiaWorkerProcess> logger)
        : this(options, logger, null) {
    }

    internal UiaWorkerProcess(
        IOptions<McpServerOptions> options,
        ILogger<UiaWorkerProcess> logger,
        string? workerPath) {
        _options = options.Value;
        _logger = logger;
        _configuredWorkerPath = workerPath ?? _options.UiaWorkerPath;
    }

    internal int? WorkerProcessId {
        get {
            lock (_processGate)
                return _process is { HasExited: false } ? _process.Id : null;
        }
    }

    public async Task<bool?> TryElementExistsAsync(
        string automationId,
        int timeoutMs,
        CancellationToken cancellationToken) {
        if (!_options.UiaWorkerEnabled || _options.Headless)
            return null;
        if (!TryResolveWorkerPath(out _)) {
            _logger.LogWarning("UIA Worker executable was not found; using the in-process compatibility path");
            return null;
        }

        var result = await ExecuteAsync<UiaWorkerProbeResult>(
            UiaWorkerProtocol.ElementExists,
            new { automationId, timeoutMs },
            GetOperationTimeout(timeoutMs),
            cancellationToken).ConfigureAwait(false);
        return result.Exists;
    }

    internal Task PingAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync<UiaWorkerPingResult>(
            UiaWorkerProtocol.Ping,
            new { },
            _options.UiaWorkerStartupTimeoutMs,
            cancellationToken);

    internal Task DelayForTestAsync(
        int delayMs,
        int timeoutMs,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<JsonElement>(
            UiaWorkerProtocol.TestDelay,
            new { delayMs },
            timeoutMs,
            cancellationToken);

    public void Dispose() {
        TaskCompletionSource<bool> completion;
        var ownsDisposal = false;
        lock (_lifecycleGate) {
            if (_disposeCompletion is null) {
                _disposed = true;
                _disposeCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                ownsDisposal = true;
            }
            completion = _disposeCompletion;
        }
        if (!ownsDisposal) {
            completion.Task.GetAwaiter().GetResult();
            return;
        }

        try {
            _shutdown.Cancel();
            KillProcess();
            _requestGate.Wait();
            KillProcess();
            _requestGate.Release();
            _requestGate.Dispose();
            _shutdown.Dispose();
        }
        finally {
            completion.TrySetResult(true);
        }
    }

    private async Task<T> ExecuteAsync<T>(
        string command,
        object arguments,
        int timeoutMs,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _shutdown.Token);
            timeout.CancelAfter(Math.Max(1, timeoutMs));
            try {
                await EnsureProcessAsync(timeout.Token).ConfigureAwait(false);
                return await SendRequestCoreAsync<T>(command, arguments, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested && !_shutdown.IsCancellationRequested) {
                KillProcess();
                throw new TimeoutException(
                    $"UIA Worker command '{command}' exceeded the {Math.Max(1, timeoutMs)}ms timeout.{FormatDiagnostics()}",
                    exception);
            }
            catch {
                KillProcess();
                throw;
            }
        }
        finally {
            _requestGate.Release();
        }
    }

    private async Task EnsureProcessAsync(CancellationToken cancellationToken) {
        lock (_processGate) {
            if (_process is { HasExited: false })
                return;
        }

        KillProcess();
        ClearDiagnostics();
        if (!TryResolveWorkerPath(out var workerPath))
            throw new FileNotFoundException("UIA Worker executable was not found.");

        var process = Process.Start(new ProcessStartInfo {
            FileName = workerPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(workerPath)!
        }) ?? throw new InvalidOperationException("Failed to start UIA Worker.");
        var stdin = new StreamWriter(
            process.StandardInput.BaseStream,
            new UTF8Encoding(false),
            4096,
            leaveOpen: true) {
            AutoFlush = true
        };

        lock (_processGate) {
            if (_disposed) {
                process.Kill(entireProcessTree: true);
                process.Dispose();
                throw new ObjectDisposedException(nameof(UiaWorkerProcess));
            }
            _process = process;
            _stdin = stdin;
            _stdout = process.StandardOutput.BaseStream;
        }
        _ = CaptureStderrAsync(process.StandardError);

        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startup.CancelAfter(Math.Max(1, _options.UiaWorkerStartupTimeoutMs));
        var ping = await SendRequestCoreAsync<UiaWorkerPingResult>(
            UiaWorkerProtocol.Ping,
            new { },
            startup.Token).ConfigureAwait(false);
        if (ping.ProtocolVersion != UiaWorkerProtocol.Version)
            throw new InvalidOperationException(
                $"UIA Worker reported protocol version {ping.ProtocolVersion}; expected {UiaWorkerProtocol.Version}.");
    }

    private async Task<T> SendRequestCoreAsync<T>(
        string command,
        object arguments,
        CancellationToken cancellationToken) {
        StreamWriter writer;
        Stream stdout;
        lock (_processGate) {
            writer = _stdin ?? throw new InvalidOperationException("UIA Worker input is unavailable.");
            stdout = _stdout ?? throw new InvalidOperationException("UIA Worker output is unavailable.");
        }

        var request = new UiaWorkerRequest {
            RequestId = Guid.NewGuid().ToString("N"),
            Command = command,
            Arguments = JsonSerializer.SerializeToElement(arguments, JsonOptions)
        };
        var json = JsonSerializer.Serialize(request, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > MaximumRequestBytes)
            throw new InvalidOperationException("UIA Worker request exceeds the configured size limit.");

        await writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        var responseLine = await ReadLineAsync(
            stdout,
            Math.Max(1, _options.UiaWorkerMaxResponseBytes),
            cancellationToken).ConfigureAwait(false)
            ?? throw new IOException($"UIA Worker closed before responding.{FormatDiagnostics()}");
        var response = JsonSerializer.Deserialize<UiaWorkerResponse>(responseLine, JsonOptions)
            ?? throw new IOException("UIA Worker returned an empty response.");
        if (response.ProtocolVersion != UiaWorkerProtocol.Version)
            throw new UiaWorkerException(
                "protocol_version_unsupported",
                $"UIA Worker returned protocol version {response.ProtocolVersion}; expected {UiaWorkerProtocol.Version}.");
        if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
            throw new UiaWorkerException("invalid_response", "UIA Worker response requestId did not match the request.");
        if (!response.Success) {
            throw new UiaWorkerException(
                response.Error?.Code ?? "uia_worker_error",
                response.Error?.Message ?? "UIA Worker returned an error.",
                response.Error?.ExceptionType);
        }
        if (response.Result.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new UiaWorkerException("invalid_response", "UIA Worker response did not contain a result.");
        return JsonSerializer.Deserialize<T>(response.Result.GetRawText(), JsonOptions)
            ?? throw new UiaWorkerException("invalid_response", "UIA Worker result could not be decoded.");
    }

    private static async Task<string?> ReadLineAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken) {
        var bytes = new List<byte>(Math.Min(maxBytes, 4096));
        var buffer = new byte[4096];
        while (true) {
            var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0)
                return bytes.Count == 0 ? null : DecodeLine(bytes);
            for (var index = 0; index < count; index++) {
                if (buffer[index] == (byte)'\n')
                    return DecodeLine(bytes);
                if (bytes.Count >= maxBytes)
                    throw new IOException("UIA Worker response exceeds the configured size limit.");
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

    private bool TryResolveWorkerPath(out string workerPath) {
        if (!string.IsNullOrWhiteSpace(_configuredWorkerPath)) {
            workerPath = Path.GetFullPath(_configuredWorkerPath);
            return File.Exists(workerPath);
        }

        var serverDirectory = Path.GetDirectoryName(typeof(UiaWorkerProcess).Assembly.Location)!;
        var published = Path.Combine(serverDirectory, "uiaworker", "Rhombus.WinFormsMcp.UiaWorker.exe");
        if (File.Exists(published)) {
            workerPath = published;
            return true;
        }

        var current = new DirectoryInfo(serverDirectory);
        while (current is not null &&
               !string.Equals(current.Name, "Rhombus.WinFormsMcp.Server", StringComparison.OrdinalIgnoreCase))
            current = current.Parent;
        if (current?.Parent is not null) {
            var relative = Path.GetRelativePath(current.FullName, serverDirectory)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var configuration = relative.Length > 1 && string.Equals(relative[0], "bin", StringComparison.OrdinalIgnoreCase)
                ? relative[1]
                : "Debug";
            var sourceBuild = Path.Combine(
                current.Parent.FullName,
                "Rhombus.WinFormsMcp.UiaWorker",
                "bin",
                configuration,
                "net8.0-windows",
                "Rhombus.WinFormsMcp.UiaWorker.exe");
            if (File.Exists(sourceBuild)) {
                workerPath = sourceBuild;
                return true;
            }
        }

        workerPath = published;
        return false;
    }

    private int GetOperationTimeout(int requestedTimeoutMs) =>
        Math.Min(
            Math.Max(1, _options.UiaWorkerRequestTimeoutMs),
            Math.Max(1, requestedTimeoutMs) + 1000);

    private async Task CaptureStderrAsync(StreamReader reader) {
        try {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line) {
                lock (_diagnosticGate) {
                    if (_stderr.Length >= MaximumDiagnosticCharacters)
                        continue;
                    var remaining = MaximumDiagnosticCharacters - _stderr.Length;
                    _stderr.AppendLine(line.Length <= remaining ? line : line[..remaining]);
                }
            }
        }
        catch (Exception exception) {
            _logger.LogDebug(exception, "UIA Worker stderr capture stopped");
        }
    }

    private string FormatDiagnostics() {
        lock (_diagnosticGate)
            return _stderr.Length == 0 ? string.Empty : $" Worker diagnostics: {_stderr}";
    }

    private void ClearDiagnostics() {
        lock (_diagnosticGate)
            _stderr.Clear();
    }

    private void KillProcess() {
        Process? process;
        StreamWriter? stdin;
        Stream? stdout;
        lock (_processGate) {
            process = _process;
            stdin = _stdin;
            stdout = _stdout;
            _process = null;
            _stdin = null;
            _stdout = null;
        }

        try {
            if (process is { HasExited: false })
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) {
            _logger.LogDebug(exception, "Failed to stop UIA Worker cleanly");
        }
        finally {
            TryDispose(stdin);
            TryDispose(stdout);
            TryDispose(process);
        }
    }

    private void TryDispose(IDisposable? disposable) {
        try {
            disposable?.Dispose();
        }
        catch (Exception exception) {
            _logger.LogDebug(exception, "Failed to release a UIA Worker resource");
        }
    }
}