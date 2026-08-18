using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Manages a pool of out-of-process RendererHost instances keyed by TFM category.
/// Each host is a long-lived process that accepts JSON render requests over stdin/stdout.
/// Processes are reused across calls and killed after an idle timeout.
/// </summary>
public sealed class RendererProcessPool : IDisposable {
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The three host TFMs we ship. Every target framework maps to one of these.
    /// </summary>
    private static readonly string[] HostTfms = { "net48", "netcoreapp3.1", "net8.0-windows" };

    private readonly IMemoryCache _cache;
    private readonly ConcurrentBag<string> _activeKeys = new();
    private readonly object _createLock = new();
    private readonly Lazy<string> _hostBasePath;
    private readonly string _configuredTfm;
    private readonly TimeSpan _renderTimeout;
    private readonly TimeSpan _startupTimeout;
    private bool _disposed;

    /// <param name="cache">Memory cache instance for managing host entries.</param>
    /// <param name="serverOptions">Strongly-typed server options (provides TFM configuration).</param>
    /// <param name="hostBasePath">
    /// Directory containing the RendererHost build output (with subdirs per TFM).
    /// If null, auto-detected relative to this assembly on first use.
    /// </param>
    public RendererProcessPool(IMemoryCache cache, IOptions<McpServerOptions> serverOptions, string? hostBasePath = null) {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configuredTfm = serverOptions?.Value?.Tfm ?? "auto";
        _renderTimeout = TimeSpan.FromMilliseconds(
            serverOptions?.Value?.RendererTimeoutMs ?? 30000);
        _startupTimeout = TimeSpan.FromMilliseconds(
            serverOptions?.Value?.RendererStartupTimeoutMs ?? 10000);
        _hostBasePath = hostBasePath != null
            ? new Lazy<string>(hostBasePath)
            : new Lazy<string>(DetectHostBasePath);
    }

    /// <summary>
    /// Render designer code using the appropriate out-of-process host for the given TFM.
    /// </summary>
    /// <param name="designerContent">Content of the .Designer.cs file.</param>
    /// <param name="companionContent">Content of the companion .cs file (optional).</param>
    /// <param name="extraAssemblyPaths">Extra assembly search paths (optional).</param>
    /// <param name="targetTfm">
    /// The TFM to render under, or "auto" to detect from csproj.
    /// When "auto", <paramref name="csprojPath"/> must be provided.
    /// </param>
    /// <param name="csprojPath">Path to the project's .csproj (used when targetTfm is "auto").</param>
    /// <returns>PNG bytes of the rendered form.</returns>
    public async Task<byte[]> RenderAsync(
        string designerContent,
        string? companionContent,
        string[]? extraAssemblyPaths,
        string targetTfm,
        string? csprojPath = null,
        CancellationToken cancellationToken = default) {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RendererProcessPool));

        var hostTfm = ResolveHostTfm(targetTfm, csprojPath);
        var entry = GetOrCreateEntry(hostTfm);

        return await entry.RenderAsync(
            designerContent,
            companionContent,
            extraAssemblyPaths,
            cancellationToken);
    }

    /// <summary>
    /// Returns the configured TFM from server options, or "auto" if not set.
    /// </summary>
    public string GetConfiguredTfm() => _configuredTfm;

    /// <summary>
    /// Detect the target framework from a .csproj file's TargetFramework(s) element.
    /// Returns the first TFM found.
    /// </summary>
    public static string DetectTfmFromCsproj(string csprojPath) {
        var doc = XDocument.Load(csprojPath);

        // Helper: search for an element by local name, ignoring XML namespace.
        // SDK-style csproj has no namespace; old-style has xmlns="http://schemas.microsoft.com/developer/msbuild/2003".
        XElement? FindByLocalName(string localName) =>
            doc.Root?.Descendants().FirstOrDefault(e =>
                string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

        // SDK-style: <TargetFramework> or <TargetFrameworks>
        var tfElem = FindByLocalName("TargetFramework") ?? FindByLocalName("TargetFrameworks");

        if (tfElem != null && !string.IsNullOrWhiteSpace(tfElem.Value)) {
            var raw = tfElem.Value.Split(';')[0].Trim();
            return raw;
        }

        // Old-style csproj: <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
        var tfvElem = FindByLocalName("TargetFrameworkVersion");
        if (tfvElem != null && !string.IsNullOrWhiteSpace(tfvElem.Value)) {
            // Convert "v4.7.2" → "net472" (strip 'v', remove dots)
            var ver = tfvElem.Value.Trim().TrimStart('v', 'V').Replace(".", "");
            return $"net{ver}";
        }

        throw new InvalidOperationException(
            $"No TargetFramework or TargetFrameworkVersion found in {csprojPath}. " +
            "You can bypass auto-detection by setting the TFM environment variable " +
            "(e.g. TFM=net48 or TFM=net8.0-windows).");
    }

    /// <summary>
    /// Map any target framework moniker to the closest host TFM we ship.
    /// </summary>
    public static string MapToHostTfm(string projectTfm) {
        var tfm = projectTfm.ToLowerInvariant().Trim();

        // .NET Framework 4.x → net48 host
        if (tfm.StartsWith("net4") && !tfm.Contains("."))
            return "net48";
        // net40, net45, net451, net452, net46, net461, net462, net47, net471, net472, net48, net481
        if (tfm.StartsWith("net") && tfm.Length <= 6 && !tfm.Contains(".") && !tfm.Contains("-")) {
            if (int.TryParse(tfm.Substring(3), out var ver) && ver >= 20 && ver < 500)
                return "net48";
        }

        // .NET Core 3.x → netcoreapp3.1 host
        if (tfm.StartsWith("netcoreapp3"))
            return "netcoreapp3.1";

        // .NET Core 1.x/2.x don't support WinForms — shouldn't happen, but fallback to net8
        if (tfm.StartsWith("netcoreapp"))
            return "net8.0-windows";

        // .NET 5+ (net5.0-windows, net6.0-windows, net7.0-windows, net8.0-windows, net9.0-windows, etc.)
        if (tfm.StartsWith("net") && tfm.Contains("."))
            return "net8.0-windows";

        // Fallback — best guess is the newest host
        return "net8.0-windows";
    }

    internal static string BuildReferenceFingerprint(IEnumerable<string>? assemblyPaths) {
        var input = new StringBuilder();
        foreach (var path in (assemblyPaths ?? [])
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)) {
            var file = new FileInfo(path);
            input.Append(path);
            input.Append('|').Append(file.Exists ? file.Length : -1);
            input.Append('|').Append(file.Exists ? file.LastWriteTimeUtc.Ticks : -1);
            input.AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.ToString())));
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;

        // Removing from cache triggers eviction callbacks which dispose HostEntries
        foreach (var key in _activeKeys) {
            _cache.Remove(key);
        }
    }

    private HostEntry GetOrCreateEntry(string hostTfm) {
        var key = $"RendererHost:{hostTfm}";
        if (_cache.TryGetValue<HostEntry>(key, out var existing))
            return existing!;

        lock (_createLock) {
            if (_cache.TryGetValue<HostEntry>(key, out existing))
                return existing!;

            var entry = new HostEntry(
                hostTfm,
                _hostBasePath.Value,
                _renderTimeout,
                _startupTimeout);
            var options = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(IdleTimeout)
                .RegisterPostEvictionCallback((k, v, reason, state) => {
                    if (v is HostEntry he)
                        he.Dispose();
                });
            _cache.Set(key, entry, options);
            _activeKeys.Add(key);
            return entry;
        }
    }

    private string ResolveHostTfm(string targetTfm, string? csprojPath) {
        if (!string.Equals(targetTfm, "auto", StringComparison.OrdinalIgnoreCase)) {
            // Check if it's already a host TFM
            foreach (var h in HostTfms) {
                if (string.Equals(targetTfm, h, StringComparison.OrdinalIgnoreCase))
                    return h;
            }
            // Otherwise map it
            return MapToHostTfm(targetTfm);
        }

        // Auto-detect from csproj
        if (string.IsNullOrEmpty(csprojPath))
            throw new ArgumentException(
                "csprojPath is required when targetTfm is 'auto'. " +
                "No .csproj file could be found. You can bypass auto-detection by setting " +
                "the TFM environment variable (e.g. TFM=net48 or TFM=net8.0-windows).");

        var projectTfm = DetectTfmFromCsproj(csprojPath);
        return MapToHostTfm(projectTfm);
    }

    private static string DetectHostBasePath() {
        // Walk up from this assembly's location to find the RendererHost build output.
        // Layout: .../<config>/<tfm>/winformsmcp.dll
        // RendererHost: .../Rhombus.WinFormsMcp.RendererHost/bin/<config>/<tfm>/
        var serverDir = Path.GetDirectoryName(typeof(RendererProcessPool).Assembly.Location)!;

        // Try sibling project in source layout first
        // server: src/Rhombus.WinFormsMcp.Server/bin/Debug/net8.0-windows/
        // host:   src/Rhombus.WinFormsMcp.RendererHost/bin/Debug/
        var parts = serverDir.Replace('\\', '/').Split('/');
        for (int i = parts.Length - 1; i >= 0; i--) {
            if (string.Equals(parts[i], "bin", StringComparison.OrdinalIgnoreCase) && i >= 2) {
                // parts[i-1] = project name, parts[i] = "bin", parts[i+1] = config
                // Go up to the parent of the project dir (e.g. src/)
                var parentDir = string.Join("/", parts.Take(i - 1));
                var config = (i + 1 < parts.Length) ? parts[i + 1] : "Debug";
                var hostBin = Path.Combine(parentDir, "Rhombus.WinFormsMcp.RendererHost", "bin", config);
                if (Directory.Exists(hostBin))
                    return hostBin;
            }
        }

        // Try relative to assembly (published layout: tools/rendererhost/)
        var publishDir = Path.Combine(serverDir, "rendererhost");
        if (Directory.Exists(publishDir))
            return publishDir;

        throw new DirectoryNotFoundException(
            $"Cannot find RendererHost build output. Looked relative to: {serverDir}. " +
            "Build the RendererHost project or set hostBasePath explicitly.");
    }

    /// <summary>
    /// Manages a single host process for one TFM category.
    /// Thread-safe: uses a SemaphoreSlim to serialize requests to one process.
    /// </summary>
    private sealed class HostEntry : IDisposable {
        private const int MaxDiagnosticCharacters = 16000;

        private readonly string _tfm;
        private readonly string _hostBasePath;
        private readonly TimeSpan _renderTimeout;
        private readonly TimeSpan _startupTimeout;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly object _stderrLock = new();
        private readonly StringBuilder _stderr = new();
        private Process? _process;
        private StreamWriter? _stdin;
        private StreamReader? _stdout;
        private string? _referenceFingerprint;
        private bool _disposed;

        public HostEntry(
            string tfm,
            string hostBasePath,
            TimeSpan renderTimeout,
            TimeSpan startupTimeout) {
            _tfm = tfm;
            _hostBasePath = hostBasePath;
            _renderTimeout = renderTimeout;
            _startupTimeout = startupTimeout;
        }

        public async Task<byte[]> RenderAsync(
            string designerContent,
            string? companionContent,
            string[]? extraAssemblyPaths,
            CancellationToken cancellationToken) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await _lock.WaitAsync(cancellationToken);
            try {
                var referenceFingerprint = BuildReferenceFingerprint(extraAssemblyPaths);
                if (_referenceFingerprint != null &&
                    !string.Equals(
                        _referenceFingerprint,
                        referenceFingerprint,
                        StringComparison.Ordinal)) {
                    // Loaded assemblies cannot be replaced in-place. Start a clean host when
                    // switching projects or when a referenced DLL has been rebuilt.
                    KillProcess();
                }
                _referenceFingerprint = referenceFingerprint;
                await EnsureProcessAsync(cancellationToken);

                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(_renderTimeout);
                var requestToken = timeoutSource.Token;
                try {
                    var request = JsonSerializer.Serialize(new {
                        designerContent,
                        companionContent,
                        extraAssemblyPaths
                    }, JsonOptions);

                    await _stdin!.WriteLineAsync(request.AsMemory(), requestToken);
                    await _stdin.FlushAsync(requestToken);
                    var responseLine = await _stdout!.ReadLineAsync(requestToken);
                    if (responseLine == null) {
                        throw new InvalidOperationException(
                            $"RendererHost ({_tfm}) closed unexpectedly.{FormatDiagnostics()}");
                    }

                    using var document = JsonDocument.Parse(responseLine);
                    var root = document.RootElement;
                    if (root.TryGetProperty("success", out var success) && success.GetBoolean()) {
                        var base64 = root.GetProperty("pngBase64").GetString()
                            ?? throw new InvalidOperationException(
                                "RendererHost returned success but no image data.");
                        return Convert.FromBase64String(base64);
                    }

                    var code = ReadString(root, "errorCode") ?? "renderer_error";
                    var error = ReadString(root, "error") ?? "Unknown renderer error";
                    var exceptionType = ReadString(root, "exceptionType");
                    var details = ReadString(root, "details");
                    throw new RendererHostException(
                        code,
                        BuildHostErrorMessage(error, exceptionType, details));
                }
                catch (OperationCanceledException exception)
                    when (!cancellationToken.IsCancellationRequested) {
                    var diagnostics = FormatDiagnostics();
                    throw new TimeoutException(
                        $"RendererHost ({_tfm}) exceeded the {_renderTimeout.TotalMilliseconds:0}ms " +
                        $"render timeout.{diagnostics}",
                        exception);
                }
            }
            catch {
                // A partial request/response makes the stream unusable. Recreate it next time.
                KillProcess();
                throw;
            }
            finally {
                _lock.Release();
            }
        }

        public void Dispose() {
            if (_disposed)
                return;
            _disposed = true;
            KillProcess();
            _lock.Dispose();
        }

        private async Task EnsureProcessAsync(CancellationToken cancellationToken) {
            if (_process != null && !_process.HasExited)
                return;

            KillProcess();
            ClearDiagnostics();

            var exePath = FindHostExe();
            var startInfo = new ProcessStartInfo {
                FileName = exePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)!
            };

            _process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start RendererHost for {_tfm}");
            _stdin = _process.StandardInput;
            _stdout = _process.StandardOutput;
            _ = CaptureStderrAsync(_process.StandardError);

            using var startupSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startupSource.CancelAfter(_startupTimeout);
            try {
                var readyLine = await _stdout.ReadLineAsync(startupSource.Token);
                if (readyLine == null) {
                    throw new InvalidOperationException(
                        $"RendererHost ({_tfm}) exited before sending ready signal.{FormatDiagnostics()}");
                }

                using var readyDocument = JsonDocument.Parse(readyLine);
                var type = readyDocument.RootElement.GetProperty("type").GetString();
                if (!string.Equals(type, "ready", StringComparison.Ordinal)) {
                    throw new InvalidOperationException(
                        $"RendererHost ({_tfm}) sent an unexpected first message: {readyLine}");
                }
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested) {
                throw new TimeoutException(
                    $"RendererHost ({_tfm}) exceeded the {_startupTimeout.TotalMilliseconds:0}ms " +
                    $"startup timeout.{FormatDiagnostics()}",
                    exception);
            }
            catch {
                KillProcess();
                throw;
            }
        }

        private async Task CaptureStderrAsync(StreamReader reader) {
            try {
                while (await reader.ReadLineAsync() is { } line) {
                    lock (_stderrLock) {
                        if (_stderr.Length < MaxDiagnosticCharacters)
                            _stderr.AppendLine(line);
                    }
                }
            }
            catch {
                // Closing or killing the host tears down the stderr pipe.
            }
        }

        private string BuildHostErrorMessage(
            string error,
            string? exceptionType,
            string? details) {
            var message = new StringBuilder($"RendererHost ({_tfm}) error: {error}");
            if (!string.IsNullOrWhiteSpace(exceptionType))
                message.Append($" [exception: {exceptionType}]");
            if (!string.IsNullOrWhiteSpace(details))
                message.AppendLine().Append(details);
            message.Append(FormatDiagnostics());
            return message.ToString();
        }

        private string FormatDiagnostics() {
            lock (_stderrLock) {
                return _stderr.Length == 0
                    ? string.Empty
                    : $"{Environment.NewLine}RendererHost stderr:{Environment.NewLine}{_stderr}";
            }
        }

        private void ClearDiagnostics() {
            lock (_stderrLock) {
                _stderr.Clear();
            }
        }

        private void KillProcess() {
            if (_process == null)
                return;

            var process = _process;
            try {
                _stdin?.Close();
            }
            catch {
                // Continue to the process termination even if closing stdin fails.
            }
            try {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch {
                // Best-effort cleanup; a dead host will still be replaced on the next call.
            }
            try {
                process.Dispose();
            }
            catch {
                // Process handles are best-effort during cancellation and shutdown.
            }
            finally {
                _process = null;
                _stdin = null;
                _stdout = null;
            }
        }

        private static string? ReadString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

        private string FindHostExe() {
            var exeName = "Rhombus.WinFormsMcp.RendererHost.exe";
            var exePath = Path.Combine(_hostBasePath, _tfm, exeName);
            if (File.Exists(exePath))
                return exePath;

            // Try without windows suffix (published layout may use short names)
            var shortTfm = _tfm.Replace("-windows", "");
            exePath = Path.Combine(_hostBasePath, shortTfm, exeName);
            if (File.Exists(exePath))
                return exePath;

            throw new FileNotFoundException(
                $"RendererHost executable not found for {_tfm}. " +
                $"Expected: {Path.Combine(_hostBasePath, _tfm, exeName)}");
        }

        private static readonly JsonSerializerOptions JsonOptions = new() {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    internal sealed class RendererHostException : InvalidOperationException {
        public RendererHostException(string code, string message)
            : base(message) {
            Code = code;
        }

        public string Code { get; }
    }
}