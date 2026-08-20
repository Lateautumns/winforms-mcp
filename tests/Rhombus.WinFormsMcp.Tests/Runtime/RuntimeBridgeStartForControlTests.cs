using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeBridge;
using Rhombus.WinFormsMcp.RuntimeBridge.Hosting;
using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Tests.Runtime;

[TestFixture]
[NonParallelizable]
public sealed class RuntimeBridgeStartForControlTests {
    [TearDown]
    public void TearDown() {
        McpRuntimeBridge.Stop();
        SynchronizationContext.SetSynchronizationContext(null);
    }

    [Test]
    public void StartForControl_NullInvoker_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => McpRuntimeBridge.StartForControl(null!));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StartForControl_DisposedControl_ThrowsObjectDisposedException() {
        var control = new Control();
        control.Dispose();

        Assert.Throws<ObjectDisposedException>(() => McpRuntimeBridge.StartForControl(control));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StartForControl_ControlWithoutHandle_ThrowsInvalidOperationException() {
        using var control = new Control();

        var exception = Assert.Throws<InvalidOperationException>(() => McpRuntimeBridge.StartForControl(control));

        Assert.That(exception!.Message, Does.Contain("Form.Shown"));
    }

    [Test]
    public void Start_WithoutFormOrWinFormsContext_FailsFast() {
        SynchronizationContext.SetSynchronizationContext(null);
        Assert.That(Application.OpenForms.Count, Is.EqualTo(0));

        var exception = Assert.Throws<InvalidOperationException>(() => McpRuntimeBridge.Start());

        Assert.That(exception!.Message, Does.Contain("StartForControl"));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StartForControl_ValidControl_StartsAndReturnsSameInstanceOnRepeat() {
        using var form = CreateSimpleForm();

        var first = McpRuntimeBridge.StartForControl(form);
        var repeated = McpRuntimeBridge.StartForControl(form);
        var legacy = McpRuntimeBridge.Start();

        Assert.Multiple(() => {
            Assert.That(first.IsRunning, Is.True);
            Assert.That(repeated, Is.SameAs(first));
            Assert.That(legacy, Is.SameAs(first));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StartForControl_AfterStop_StartsNewHost() {
        using var form = CreateSimpleForm();

        var first = McpRuntimeBridge.StartForControl(form);
        McpRuntimeBridge.Stop();
        var second = McpRuntimeBridge.StartForControl(form);

        Assert.Multiple(() => {
            Assert.That(first.IsRunning, Is.False);
            Assert.That(second.IsRunning, Is.True);
            Assert.That(second, Is.Not.SameAs(first));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StartForControl_DispatchesBackgroundRequestToUiThread() {
        using var ui = RunFormOnUiThread(out var form);
        var dispatcher = new UiThreadDispatcher(form);

        var observedThreadId = Task.Run(() =>
            dispatcher.InvokeAsync(() => Environment.CurrentManagedThreadId, CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        Assert.That(observedThreadId, Is.EqualTo(ui.ThreadId));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StartForControl_HelloReportsProtocolAndCapabilities() {
        using var ui = RunFormOnUiThread(out var form);
        var host = McpRuntimeBridge.StartForControl(form);
        try {
            var hello = Task.Run(() => SendRequestAsync(host.PipeName, RuntimeBridgeProtocol.Hello))
                .GetAwaiter()
                .GetResult();

            Assert.Multiple(() => {
                Assert.That(hello.Success, Is.True);
                Assert.That(hello.Result.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(RuntimeBridgeProtocol.Version));
                Assert.That(hello.Result.GetProperty("bridgeInstanceId").GetString(), Is.Not.Empty);
                Assert.That(hello.Result.GetProperty("process").GetProperty("bridgeVersion").GetString(), Is.Not.Empty);
                Assert.That(
                    hello.Result.GetProperty("capabilities").EnumerateArray().Select(item => item.GetString()),
                    Does.Contain("uiThreadSnapshots"));
            });
        }
        finally {
            McpRuntimeBridge.Stop();
        }
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StartForControl_ControlTreeRequestReturnsRealControls() {
        using var ui = RunFormOnUiThread(out var form);
        var host = McpRuntimeBridge.StartForControl(form);
        try {
            var response = Task.Run(() => SendRequestAsync(host.PipeName, RuntimeBridgeProtocol.GetControlTree))
                .GetAwaiter()
                .GetResult();

            Assert.Multiple(() => {
                Assert.That(response.Success, Is.True);
                Assert.That(ContainsControlName(response.Result, "bridgeTestForm"), Is.True);
                Assert.That(ContainsControlName(response.Result, "testButton"), Is.True);
            });
        }
        finally {
            McpRuntimeBridge.Stop();
        }
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StartForControl_InvalidatedControlFailsWithoutPipeThreadAccess() {
        var control = new Control { Name = "doomedControl" };
        var dispatcher = new UiThreadDispatcher(control);
        control.Dispose();

        var callbackRan = false;
        var task = dispatcher.InvokeAsync(() => {
            callbackRan = true;
            return 42;
        }, CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception!.InnerException, Is.TypeOf<ObjectDisposedException>());
            Assert.That(callbackRan, Is.False);
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StartForControl_DestroyedHandleFailsWithoutPipeThreadAccess() {
        using var control = new HandleLifecycleControl { Name = "handlelessControl" };
        control.CreateHandleForTest();
        var dispatcher = new UiThreadDispatcher(control);
        control.DestroyHandleForTest();

        var callbackRan = false;
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Task.Run(async () => await dispatcher.InvokeAsync(() => {
                callbackRan = true;
                return 42;
            }, CancellationToken.None)).GetAwaiter().GetResult());

        Assert.Multiple(() => {
            Assert.That(exception!.Message, Does.Contain("handle"));
            Assert.That(callbackRan, Is.False);
        });
    }

    [Test]
    public void StartForControl_NoUiTargetFailsRequestExplicitly() {
        SynchronizationContext.SetSynchronizationContext(null);
        var dispatcher = new UiThreadDispatcher(null);

        var task = dispatcher.InvokeAsync(() => 1, CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception!.InnerException, Is.TypeOf<InvalidOperationException>());
        });
    }

    private static Form CreateSimpleForm() {
        var form = new Form {
            Name = "bridgeTestForm",
            Text = "Bridge Test",
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-32000, -32000),
            ShowInTaskbar = false
        };
        form.Controls.Add(new Button {
            Name = "testButton",
            Text = "Test",
            Location = new System.Drawing.Point(12, 12),
            Size = new System.Drawing.Size(120, 30)
        });
        form.Show();
        return form;
    }

    private static UiThreadContext RunFormOnUiThread(out Form form) {
        var created = new Form {
            Name = "bridgeTestForm",
            Text = "Bridge Test",
            ShowInTaskbar = false
        };
        created.Controls.Add(new Button {
            Name = "testButton",
            Text = "Test",
            Location = new System.Drawing.Point(12, 12),
            Size = new System.Drawing.Size(120, 30)
        });
        form = created;
        var thread = new Thread(() => Application.Run(created)) {
            IsBackground = true,
            Name = "bridge-test-ui"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        for (var attempt = 0; attempt < 200 && !form.IsHandleCreated; attempt++)
            Thread.Sleep(25);
        if (!form.IsHandleCreated)
            throw new InvalidOperationException("The test form handle was not created in time.");
        return new UiThreadContext(form, thread);
    }

    private sealed class HandleLifecycleControl : Control {
        public void CreateHandleForTest() => _ = Handle;

        public void DestroyHandleForTest() => DestroyHandle();
    }

    private sealed class UiThreadContext : IDisposable {
        public UiThreadContext(Form form, Thread thread) {
            Form = form;
            Thread = thread;
        }

        public Form Form { get; }
        public Thread Thread { get; }
        public int ThreadId => Thread.ManagedThreadId;

        public void Dispose() {
            if (!Form.IsDisposed && Form.IsHandleCreated) {
                try {
                    Form.Invoke((Action)(() => Form.Close()));
                }
                catch (InvalidOperationException) {
                    // ObjectDisposedException derives from InvalidOperationException
                    // and is covered by this catch.
                }
            }

            if (!Thread.Join(TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException("The UI thread did not exit after the form was closed.");
        }
    }

    private static async Task<RuntimeResponse> SendRequestAsync(
        string pipeName,
        string command,
        string? bridgeInstanceId = null) {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await pipe.ConnectAsync(timeout.Token);
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) {
            AutoFlush = true
        };
        var request = new RuntimeRequest {
            ProtocolVersion = RuntimeBridgeProtocol.Version,
            RequestId = Guid.NewGuid().ToString("N"),
            Command = command,
            Pid = Environment.ProcessId,
            BridgeInstanceId = bridgeInstanceId,
            Arguments = JsonSerializer.SerializeToElement(new { })
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
        var responseLine = await reader.ReadLineAsync()
            ?? throw new IOException("RuntimeBridge closed the pipe before responding.");
        return JsonSerializer.Deserialize<RuntimeResponse>(responseLine, JsonOptions)
            ?? throw new IOException("RuntimeBridge returned an empty response.");
    }

    private static bool ContainsControlName(JsonElement result, string name) {
        if (!result.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
            return false;
        return roots.EnumerateArray().Any(root => NodeOrChildHasName(root, name));
    }

    private static bool NodeOrChildHasName(JsonElement node, string name) {
        if (node.TryGetProperty("summary", out var summary) &&
            summary.TryGetProperty("identity", out var identity) &&
            identity.TryGetProperty("name", out var nodeName) &&
            nodeName.ValueKind == JsonValueKind.String &&
            string.Equals(nodeName.GetString(), name, StringComparison.Ordinal))
            return true;
        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array) {
            foreach (var child in children.EnumerateArray()) {
                if (NodeOrChildHasName(child, name))
                    return true;
            }
        }

        return false;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };
}