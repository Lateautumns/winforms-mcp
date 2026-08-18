using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Rhombus.WinFormsMcp.Server.Automation;

/// <summary>
/// Launches, attaches to, observes, and terminates target processes.
/// </summary>
internal sealed class ProcessService : IDisposable {
    private readonly Dictionary<int, Process> _processes = [];
    private readonly ConcurrentDictionary<int, StringBuilder> _stderrBuffers = new();
    private readonly ConcurrentDictionary<int, IntPtr> _nativeProcessHandles = new();
    private readonly object _sync = new();
    private readonly HeadlessDesktopService _desktopService;
    private readonly ILogger<AutomationHelper>? _logger;

    public ProcessService(
        HeadlessDesktopService desktopService,
        ILogger<AutomationHelper>? logger) {
        _desktopService = desktopService;
        _logger = logger;
    }

    public Process LaunchApp(string path, string? arguments = null, string? workingDirectory = null) {
        _logger?.LogInformation("Launching process: {Path}", path);
        Process process;

        if (_desktopService.Headless && _desktopService.HiddenDesktop != IntPtr.Zero) {
            var commandLine = string.IsNullOrEmpty(arguments)
                ? $"\"{path}\""
                : $"\"{path}\" {arguments}";
            var result = NativeMethods.LaunchOnDesktop(
                HeadlessDesktopService.DesktopName,
                commandLine,
                workingDirectory);
            if (result.Pid < 0)
                throw new InvalidOperationException($"Failed to launch {path} on hidden desktop");

            try {
                process = Process.GetProcessById(result.Pid);
            }
            catch {
                NativeMethods.CloseNativeHandle(result.ProcessHandle);
                result.Stderr?.Dispose();
                throw;
            }

            _nativeProcessHandles[result.Pid] = result.ProcessHandle;
            _desktopService.TrackHiddenProcess(result.Pid);
            CaptureHiddenProcessStderr(result.Pid, result.Stderr);
        }
        else {
            var startInfo = new ProcessStartInfo {
                FileName = path,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to launch {path}");
            _desktopService.TrackDefaultDesktopProcess(process.Id);
            CaptureProcessStderr(process);
        }

        try {
            process.WaitForInputIdle(5000);
        }
        catch (InvalidOperationException) {
            // Console applications do not have an input-idle state.
        }

        lock (_sync) {
            _processes[process.Id] = process;
        }
        return process;
    }

    public Process AttachToProcess(int pid) {
        _logger?.LogInformation("Attaching to process: {Pid}", pid);
        var process = Process.GetProcessById(pid);
        TrackAttachedProcess(process);
        return process;
    }

    public Process AttachToProcessByName(string name) {
        var processes = Process.GetProcessesByName(name);
        if (processes.Length == 0)
            throw new InvalidOperationException($"No process found with name: {name}");

        var process = processes[0];
        TrackAttachedProcess(process);
        return process;
    }

    public void CloseApp(int pid, bool force = false) {
        _logger?.LogInformation("Closing process: {Pid}", pid);
        Process? process;
        lock (_sync) {
            _processes.TryGetValue(pid, out process);
        }

        if (process == null)
            return;

        try {
            if (force) {
                process.Kill();
            }
            else {
                process.CloseMainWindow();
                process.WaitForExit(5000);
                if (!process.HasExited)
                    process.Kill();
            }
        }
        catch {
            // Process may already have exited or become inaccessible.
        }
        finally {
            lock (_sync) {
                _processes.Remove(pid);
            }
            ReleaseProcessResources(pid);
        }
    }

    public Dictionary<string, object?> GetProcessStatus(int pid) {
        Process? process;
        lock (_sync) {
            _processes.TryGetValue(pid, out process);
        }
        process ??= TryGetProcessById(pid);

        var result = new Dictionary<string, object?>();
        if (process == null) {
            result["isRunning"] = false;
            result["hasExited"] = true;
            result["exitCode"] = null;
            result["responding"] = false;
            result["mainWindowTitle"] = "";
            result["stderr"] = GetStderr(pid);
            return result;
        }

        bool hasExited;
        try {
            hasExited = process.HasExited;
        }
        catch {
            hasExited = true;
        }

        result["isRunning"] = !hasExited;
        result["hasExited"] = hasExited;
        if (hasExited) {
            try {
                result["exitCode"] = process.ExitCode;
            }
            catch {
                result["exitCode"] = _nativeProcessHandles.TryGetValue(pid, out var handle)
                    ? NativeMethods.GetExitCode(handle)
                    : null;
            }
            result["responding"] = false;
            result["mainWindowTitle"] = "";
        }
        else {
            result["exitCode"] = null;
            try {
                result["responding"] = process.Responding;
            }
            catch {
                result["responding"] = false;
            }
            try {
                result["mainWindowTitle"] = process.MainWindowTitle ?? "";
            }
            catch {
                result["mainWindowTitle"] = "";
            }
        }

        result["stderr"] = GetStderr(pid);
        return result;
    }

    internal string GetStderr(int pid) =>
        _stderrBuffers.TryGetValue(pid, out var buffer) ? buffer.ToString() : "";

    public void Dispose() {
        lock (_sync) {
            foreach (var process in _processes.Values) {
                try {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch {
                    // Best-effort cleanup during server shutdown.
                }
                process.Dispose();
            }
            _processes.Clear();
        }

        _stderrBuffers.Clear();
        foreach (var handle in _nativeProcessHandles.Values)
            NativeMethods.CloseNativeHandle(handle);
        _nativeProcessHandles.Clear();
    }

    private void TrackAttachedProcess(Process process) {
        _desktopService.TrackDefaultDesktopProcess(process.Id);
        lock (_sync) {
            _processes[process.Id] = process;
        }
    }

    private void CaptureProcessStderr(Process process) {
        var buffer = new StringBuilder();
        _stderrBuffers[process.Id] = buffer;
        process.ErrorDataReceived += (_, eventArgs) => {
            if (eventArgs.Data != null)
                buffer.AppendLine(eventArgs.Data);
        };
        process.BeginErrorReadLine();
    }

    private void CaptureHiddenProcessStderr(int pid, StreamReader? reader) {
        if (reader == null)
            return;

        var buffer = new StringBuilder();
        _stderrBuffers[pid] = buffer;
        _ = Task.Run(async () => {
            try {
                while (await reader.ReadLineAsync() is { } line)
                    buffer.AppendLine(line);
            }
            catch {
                // The pipe is expected to close when the process exits.
            }
            finally {
                reader.Dispose();
            }
        });
    }

    private void ReleaseProcessResources(int pid) {
        _stderrBuffers.TryRemove(pid, out _);
        _desktopService.RemoveProcess(pid);
        if (_nativeProcessHandles.TryRemove(pid, out var nativeHandle))
            NativeMethods.CloseNativeHandle(nativeHandle);
    }

    private static Process? TryGetProcessById(int pid) {
        try {
            return Process.GetProcessById(pid);
        }
        catch {
            return null;
        }
    }
}