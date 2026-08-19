using System.Diagnostics;

using FlaUI.Core.AutomationElements;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Rhombus.WinFormsMcp.Server;
using Rhombus.WinFormsMcp.Server.Automation;
using Rhombus.WinFormsMcp.Server.Automation.UiaWorker;
using Rhombus.WinFormsMcp.Server.Tools.Inspection;

namespace Rhombus.WinFormsMcp.Tests.Runtime;

[TestFixture]
[NonParallelizable]
public sealed class UiaWorkerProcessTests {
    [Test]
    [Timeout(10000)]
    public async Task WorkerStartsAndAnswersPing() {
        using var worker = CreateWorker();

        await worker.PingAsync();

        Assert.That(worker.WorkerProcessId, Is.Not.Null);
    }

    [Test]
    [Timeout(10000)]
    public async Task ElementExistsUsesIsolatedWorkerAndReturnsFalseForMissingElement() {
        using var worker = CreateWorker();

        var result = await worker.TryElementExistsAsync(
            $"missing-{Guid.NewGuid():N}",
            100,
            CancellationToken.None);

        Assert.That(result, Is.False);
    }

    [Test]
    [Timeout(10000)]
    public async Task TimedOutWorkerCommandKillsProcessAndNextCallRebuildsIt() {
        using var worker = CreateWorker();
        await worker.PingAsync();
        var firstProcessId = worker.WorkerProcessId;

        Assert.That(
            async () => await worker.DelayForTestAsync(2000, 50),
            Throws.TypeOf<TimeoutException>());
        Assert.That(worker.WorkerProcessId, Is.Null);

        await worker.PingAsync();
        Assert.That(worker.WorkerProcessId, Is.Not.Null.And.Not.EqualTo(firstProcessId));
    }

    [Test]
    [Timeout(10000)]
    public async Task DisposeTerminatesWorkerProcess() {
        var worker = CreateWorker();
        await worker.PingAsync();
        var processId = worker.WorkerProcessId;
        Assert.That(processId, Is.Not.Null);

        worker.Dispose();

        for (var attempt = 0; attempt < 20 && IsRunning(processId!.Value); attempt++)
            await Task.Delay(25);
        Assert.That(IsRunning(processId.Value), Is.False);
    }

    [Test]
    [Timeout(10000)]
    public async Task ConcurrentRequestsReuseOneWorkerProcess() {
        using var worker = CreateWorker();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => worker.PingAsync()));

        Assert.That(worker.WorkerProcessId, Is.Not.Null);
    }

    [Test]
    [Timeout(10000)]
    public async Task DisposeCancelsActiveRequestWithoutLeavingWorker() {
        var worker = CreateWorker();
        var request = worker.DelayForTestAsync(5000, 8000);
        var processId = await WaitForWorkerProcessAsync(worker);

        var dispose = Task.Run(worker.Dispose);
        var completed = await Task.WhenAny(dispose, Task.Delay(3000));
        Assert.That(completed, Is.SameAs(dispose));
        await dispose;
        try {
            await request;
            Assert.Fail("The active worker request completed after disposal.");
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or IOException or ObjectDisposedException) {
        }

        Assert.That(IsRunning(processId), Is.False);
    }

    [Test]
    [Timeout(10000)]
    public async Task ConcurrentDisposeCallsAreIdempotent() {
        var worker = CreateWorker();
        await worker.PingAsync();
        var processId = worker.WorkerProcessId;

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(worker.Dispose)));

        Assert.Multiple(() => {
            Assert.That(worker.WorkerProcessId, Is.Null);
            Assert.That(IsRunning(processId!.Value), Is.False);
        });
        Assert.DoesNotThrow(worker.Dispose);
    }

    [Test]
    [Timeout(10000)]
    public async Task ElementExistsToolUsesWorkerWithoutCallingInProcessAutomation() {
        using var worker = CreateWorker();
        var session = new TrackingSessionManager();
        var handler = new ElementExistsToolHandler(session, worker);
        var automationId = $"missing-{Guid.NewGuid():N}";

        var result = await handler.ExecuteAsync(
            System.Text.Json.JsonSerializer.SerializeToElement(new { automationId }),
            CancellationToken.None);

        Assert.That(result.GetProperty("exists").GetBoolean(), Is.False);
        Assert.That(session.GetAutomationCalls, Is.Zero);
    }

    [Test]
    public async Task HeadlessModeKeepsCompatibilityPathWithoutStartingWorker() {
        using var worker = CreateWorker(headless: true);

        var result = await worker.TryElementExistsAsync("anything", 100, CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(result, Is.Null);
            Assert.That(worker.WorkerProcessId, Is.Null);
        });
    }

    private static UiaWorkerProcess CreateWorker(bool headless = false) {
        var workerPath = FindWorkerPath();
        return new UiaWorkerProcess(
            Options.Create(new McpServerOptions {
                Headless = headless,
                UiaWorkerEnabled = true,
                UiaWorkerStartupTimeoutMs = 2000,
                UiaWorkerRequestTimeoutMs = 3000,
                UiaWorkerMaxResponseBytes = 1024 * 1024
            }),
            NullLogger<UiaWorkerProcess>.Instance,
            workerPath);
    }

    private static async Task<int> WaitForWorkerProcessAsync(UiaWorkerProcess worker) {
        for (var attempt = 0; attempt < 100; attempt++) {
            if (worker.WorkerProcessId is { } processId)
                return processId;
            await Task.Delay(10);
        }

        Assert.Fail("UIA Worker did not start.");
        return 0;
    }

    private static string FindWorkerPath() {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current is not null) {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "Rhombus.WinFormsMcp.UiaWorker",
                "bin",
                "Release",
                "net8.0-windows",
                "Rhombus.WinFormsMcp.UiaWorker.exe");
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        Assert.Fail("The UIA Worker build output was not found.");
        return string.Empty;
    }

    private static bool IsRunning(int processId) {
        try {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException) {
            return false;
        }
        catch (InvalidOperationException) {
            return false;
        }
    }

    private sealed class TrackingSessionManager : ISessionManager {
        public int GetAutomationCalls { get; private set; }

        public IAutomationHelper GetAutomation() {
            GetAutomationCalls++;
            throw new InvalidOperationException("The in-process UIA path should not be called.");
        }

        public string CacheElement(AutomationElement element) => throw new NotSupportedException();

        public AutomationElement? GetElement(string elementId) => throw new NotSupportedException();

        public void ClearElement(string elementId) => throw new NotSupportedException();

        public void CacheProcess(int pid, object context) => throw new NotSupportedException();

        public void Dispose() {
        }
    }
}