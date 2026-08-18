using System.Collections;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeBridge;
using Rhombus.WinFormsMcp.RuntimeBridge.Inspection;
using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Tests.Runtime;

[TestFixture]
[NonParallelizable]
public sealed class RuntimeBridgeLifecycleTests {
    [Test]
    [Apartment(ApartmentState.STA)]
    public void ControlIdentityRegistry_ReturnsStableIdWithoutKeepingControlAlive() {
        var registry = new ControlIdentityRegistry();

        var id = RegisterEphemeralControl(registry, out var controlReference);
        Assert.That(ControlIsResolvable(registry, id), Is.True);

        AssertCollected(controlReference);
        registry.ForgetDisposed();

        Assert.That(registry.TryGet(id, out _), Is.False);
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void ControlIdentityRegistry_ReusesIdWhileControlIsAlive() {
        var registry = new ControlIdentityRegistry();
        using var control = new Button { Name = "saveButton" };

        var first = registry.GetOrCreateId(control);
        var second = registry.GetOrCreateId(control);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void ControlIdentityRegistry_DisposedControlIsRemoved() {
        var registry = new ControlIdentityRegistry();
        var control = new Control { Name = "disposedControl" };
        var id = registry.GetOrCreateId(control);

        control.Dispose();
        registry.ForgetDisposed();

        Assert.That(registry.TryGet(id, out _), Is.False);
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void ControlIdentityRegistry_DoesNotReuseDisposedIdentity() {
        var registry = new ControlIdentityRegistry();
        var firstControl = new Control { Name = "firstControl" };
        var firstId = registry.GetOrCreateId(firstControl);
        firstControl.Dispose();
        registry.ForgetDisposed();

        using var secondControl = new Control { Name = "secondControl" };
        var secondId = registry.GetOrCreateId(secondControl);

        Assert.That(secondId, Is.Not.EqualTo(firstId));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void ControlIdentityRegistry_ConcurrentGetOrCreateReturnsSingleId() {
        var registry = new ControlIdentityRegistry();
        using var control = new Control { Name = "sharedControl" };

        var ids = Enumerable.Range(0, 64)
            .AsParallel()
            .Select(_ => registry.GetOrCreateId(control))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(ids, Is.EqualTo(new[] { "ctrl_1" }));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void ControlIdentityRegistry_CleanupRemovesDeadReferences() {
        var registry = new ControlIdentityRegistry();
        var id = RegisterEphemeralControl(registry, out var controlReference);
        Assert.That(RegisteredControlCount(registry), Is.EqualTo(1));

        AssertCollected(controlReference);
        registry.ForgetDisposed();

        Assert.Multiple(() => {
            Assert.That(registry.TryGet(id, out _), Is.False);
            Assert.That(RegisteredControlCount(registry), Is.EqualTo(0));
        });
    }

    [Test]
    [Timeout(10000)]
    public async Task RuntimeBridgeHost_StopWithoutClientCompletesQuickly() {
        var host = new RuntimeBridgeHost(new RuntimeBridgeOptions { PipeName = CreatePipeName() }, null);
        host.Start();

        await AwaitDisposeAsync(host.StopAsync());

        Assert.That(host.IsRunning, Is.False);
        Assert.DoesNotThrow(host.Dispose);
    }

    [Test]
    [Timeout(10000)]
    public async Task RuntimeBridgeHost_DisposeClosesConnectedRequestAndStopsListener() {
        var pipeName = CreatePipeName();
        var host = new RuntimeBridgeHost(new RuntimeBridgeOptions { PipeName = pipeName }, null);
        host.Start();

        using var pipe = await ConnectPipeAsync(pipeName);
        var disposeTask = Task.Run(host.Dispose);

        await AwaitDisposeAsync(disposeTask);
        var clientObservedDisconnect = await PipeClosedAsync(pipe);

        Assert.Multiple(() => {
            Assert.That(host.IsRunning, Is.False);
            Assert.That(clientObservedDisconnect, Is.True);
        });
    }

    [Test]
    [Timeout(10000)]
    public async Task RuntimeBridgeHost_DisposeIsIdempotentUnderConcurrentCalls() {
        var host = new RuntimeBridgeHost(new RuntimeBridgeOptions { PipeName = CreatePipeName() }, null);
        host.Start();

        var disposals = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(host.Dispose))
            .ToArray();

        await AwaitDisposeAsync(Task.WhenAll(disposals));

        Assert.That(host.IsRunning, Is.False);
        Assert.DoesNotThrow(host.Dispose);
    }

    [Test]
    [Timeout(10000)]
    public async Task RuntimeBridgeHost_RestartsOnSamePipeAfterStop() {
        var pipeName = CreatePipeName();
        var first = new RuntimeBridgeHost(new RuntimeBridgeOptions { PipeName = pipeName }, null);
        first.Start();
        await SendRequestAsync(pipeName, RuntimeBridgeProtocol.GetStatus);
        await first.StopAsync();

        var second = new RuntimeBridgeHost(new RuntimeBridgeOptions { PipeName = pipeName }, null);
        second.Start();
        try {
            var response = await SendRequestAsync(pipeName, RuntimeBridgeProtocol.GetStatus);

            Assert.That(response.Success, Is.True);
        }
        finally {
            await second.StopAsync();
        }
    }

    [Test]
    [Timeout(10000)]
    public async Task RuntimeBridgeHost_ConcurrentStartAndDisposeDoesNotDeadlock() {
        var host = new RuntimeBridgeHost(new RuntimeBridgeOptions { PipeName = CreatePipeName() }, null);
        var tasks = Enumerable.Range(0, 16)
            .Select(index => Task.Run(() => {
                if ((index % 2) == 0) {
                    try {
                        host.Start();
                    }
                    catch (ObjectDisposedException) {
                    }
                }
                else {
                    host.Dispose();
                }
            }))
            .ToArray();

        await AwaitDisposeAsync(Task.WhenAll(tasks));

        Assert.That(host.IsRunning, Is.False);
    }

    [Test]
    [Timeout(10000)]
    public async Task RuntimeBridgeHost_RemainsUsableBeforeGracefulDispose() {
        var pipeName = CreatePipeName();
        var host = new RuntimeBridgeHost(new RuntimeBridgeOptions { PipeName = pipeName }, null);
        host.Start();

        try {
            var response = await SendRequestAsync(pipeName, RuntimeBridgeProtocol.GetStatus);

            Assert.Multiple(() => {
                Assert.That(response.Success, Is.True);
                Assert.That(response.ProtocolVersion, Is.EqualTo(RuntimeBridgeProtocol.Version));
                Assert.That(response.Result.GetProperty("available").GetBoolean(), Is.True);
            });
        }
        finally {
            host.Dispose();
        }
    }

    [Test]
    [Timeout(10000)]
    public async Task McpRuntimeBridge_StopIsRepeatableAndAllowsRestartOnSamePipe() {
        var pipeName = CreatePipeName();
        try {
            var first = McpRuntimeBridge.Start(new RuntimeBridgeOptions { PipeName = pipeName });
            await SendRequestAsync(pipeName, RuntimeBridgeProtocol.GetStatus);
            await McpRuntimeBridge.StopAsync();
            McpRuntimeBridge.Stop();

            var second = McpRuntimeBridge.Start(new RuntimeBridgeOptions { PipeName = pipeName });
            try {
                var response = await SendRequestAsync(pipeName, RuntimeBridgeProtocol.GetStatus);

                Assert.Multiple(() => {
                    Assert.That(first, Is.Not.SameAs(second));
                    Assert.That(response.Success, Is.True);
                });
            }
            finally {
                await McpRuntimeBridge.StopAsync();
            }
        }
        finally {
            McpRuntimeBridge.Stop();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string RegisterEphemeralControl(
        ControlIdentityRegistry registry,
        out WeakReference<Control> controlReference) {
        var control = new Control { Name = "temporaryControl" };
        var id = registry.GetOrCreateId(control);
        controlReference = new WeakReference<Control>(control);
        return id;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ControlIsResolvable(ControlIdentityRegistry registry, string id) =>
        registry.TryGet(id, out _);

    private static int RegisteredControlCount(ControlIdentityRegistry registry) {
        var field = typeof(ControlIdentityRegistry).GetField(
            "_controls",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(ControlIdentityRegistry), "_controls");
        return ((ICollection)field.GetValue(registry)!).Count;
    }

    private static void AssertCollected(WeakReference<Control> controlReference) {
        for (var attempt = 0; attempt < 10; attempt++) {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (!controlReference.TryGetTarget(out _))
                return;
        }

        Assert.Fail("ControlIdentityRegistry kept the registered control alive.");
    }

    private static async Task AwaitDisposeAsync(Task disposeTask) {
        var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.That(completed, Is.SameAs(disposeTask));
        await disposeTask;
    }

    private static async Task<NamedPipeClientStream> ConnectPipeAsync(string pipeName) {
        var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await pipe.ConnectAsync(timeout.Token);
            return pipe;
        }
        catch {
            pipe.Dispose();
            throw;
        }
    }

    private static async Task<RuntimeResponse> SendRequestAsync(string pipeName, string command) {
        using var pipe = await ConnectPipeAsync(pipeName);
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) {
            AutoFlush = true
        };
        var request = new RuntimeRequest {
            ProtocolVersion = RuntimeBridgeProtocol.Version,
            RequestId = Guid.NewGuid().ToString("N"),
            Command = command,
            Pid = Environment.ProcessId,
            Arguments = JsonSerializer.SerializeToElement(new { })
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
        var responseLine = await reader.ReadLineAsync()
            ?? throw new IOException("RuntimeBridgeHost closed the pipe before responding.");
        var response = JsonSerializer.Deserialize<RuntimeResponse>(responseLine, JsonOptions)
            ?? throw new IOException("RuntimeBridgeHost returned an empty response.");
        Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
        return response;
    }

    private static async Task<bool> PipeClosedAsync(NamedPipeClientStream pipe) {
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
        var readTask = reader.ReadLineAsync();
        var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(1)));
        if (completed != readTask)
            return false;

        try {
            return await readTask is null;
        }
        catch (IOException) {
            return true;
        }
        catch (ObjectDisposedException) {
            return true;
        }
    }

    private static string CreatePipeName() => $"winformsmcp-test-{Guid.NewGuid():N}";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };
}