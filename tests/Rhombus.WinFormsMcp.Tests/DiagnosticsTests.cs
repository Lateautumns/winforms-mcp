using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeBridge;
using Rhombus.WinFormsMcp.RuntimeBridge.Diagnostics;
using Rhombus.WinFormsMcp.RuntimeContracts;
using Rhombus.WinFormsMcp.Server.Diagnostics;

namespace Rhombus.WinFormsMcp.Tests;

[TestFixture]
public sealed class DiagnosticsTests {
    [Test]
    public void ScreenshotDiff_ReportsDeterministicChangedRegion() {
        var before = CreatePng(Color.White);
        var after = CreatePng(Color.White, (1, 1, Color.Black));
        var service = new ScreenshotDiffService();

        var result = service.Compare(
            null,
            null,
            Convert.ToBase64String(before),
            Convert.ToBase64String(after),
            maxRegions: 10,
            pixelThreshold: 0,
            CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(result.DimensionsMatch, Is.True);
            Assert.That(result.ChangedPixelCount, Is.EqualTo(1));
            Assert.That(result.ChangedPixelRatio, Is.EqualTo(0.25d));
            Assert.That(result.ChangedBounds.X, Is.EqualTo(1));
            Assert.That(result.ChangedBounds.Y, Is.EqualTo(1));
            Assert.That(result.ChangedRegions, Has.Count.EqualTo(1));
            Assert.That(result.ChangedRegions[0].PixelCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ScreenshotDiff_RejectsOversizedAndMismatchedInputs() {
        var service = new ScreenshotDiffService();
        var png = Convert.ToBase64String(CreatePng(Color.White));

        Assert.Throws<ArgumentException>(() => service.Compare(
            null, null, png, null, 10, 0, CancellationToken.None));
        Assert.Throws<ArgumentException>(() => service.Compare(
            null, null, "not-base64", png, 10, 0, CancellationToken.None));
    }

    [Test]
    public void LayoutRules_ReturnEvidenceAndRespectDiagnosticBound() {
        var parent = Record("parent", null, new RectSnapshot { Width = 100, Height = 100 }, new SizeSnapshot { Width = 100, Height = 100 });
        var left = Record("left", "parent", new RectSnapshot { X = 10, Y = 10, Width = 60, Height = 40 }, new SizeSnapshot { Width = 100, Height = 100 });
        left.TabStop = true;
        var right = Record("right", "parent", new RectSnapshot { X = 40, Y = 20, Width = 60, Height = 40 }, new SizeSnapshot { Width = 100, Height = 100 });
        right.TabStop = true;
        right.Layout.DeviceDpi = 120;
        right.Layout.ScaleFactor = 1;
        right.Bindings.Add(new ControlBindingSnapshot {
            Property = "Text",
            DataSourcePresent = false,
            DataSourceUpdateMode = "OnPropertyChanged"
        });

        var result = ControlDiagnosticRules.Analyze(
            [parent, left, right],
            ["layout", "dpi", "bindings"],
            maxNodes: 3,
            maxDiagnostics: 20,
            traversalTruncated: false,
            CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("control_overlap"));
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("invalid_dpi_scale"));
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("binding_data_source_missing"));
            Assert.That(result.Diagnostics.All(item => item.Evidence.Count > 0), Is.True);
            Assert.That(result.Diagnostics.All(item => !string.IsNullOrWhiteSpace(item.ControlId)), Is.True);
        });

        var bounded = ControlDiagnosticRules.Analyze(
            [parent, left, right],
            ["layout", "dpi", "bindings"],
            maxNodes: 3,
            maxDiagnostics: 1,
            traversalTruncated: false,
            CancellationToken.None);
        Assert.That(bounded.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(bounded.Truncated, Is.True);
    }

    [Test]
    public void LayoutRules_SiblingLimitMarksResultTruncated() {
        var parent = Record("parent", null, new RectSnapshot { Width = 100, Height = 100 }, new SizeSnapshot { Width = 100, Height = 100 });
        var left = Record("left", "parent", new RectSnapshot { X = 10, Y = 10, Width = 60, Height = 40 }, new SizeSnapshot { Width = 100, Height = 100 });
        left.TabStop = true;
        var right = Record("right", "parent", new RectSnapshot { X = 40, Y = 20, Width = 60, Height = 40 }, new SizeSnapshot { Width = 100, Height = 100 });
        right.TabStop = true;

        var result = ControlDiagnosticRules.Analyze(
            [parent, left, right],
            ["layout"],
            maxNodes: 3,
            maxDiagnostics: 1,
            traversalTruncated: false,
            CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("control_overlap"));
            Assert.That(result.Truncated, Is.True);
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void EventTrace_UsesRingBufferPagingAndDetachesOnStop() {
        using var button = new Button { Name = "traceButton", Text = "Trace" };
        using var registry = new RuntimeEventTraceRegistry(new RuntimeBridgeOptions {
            MaxEventTraceEvents = 2,
            MaxEventTraceDurationMs = 10_000
        });
        var trace = registry.Start(
            [new RuntimeEventTraceRegistry.TraceControlTarget {
                Control = button,
                ControlId = "ctrl_1",
                ControlName = button.Name,
                ControlType = button.GetType().FullName!,
                ControlPath = "Form/traceButton"
            }],
            ["Click"],
            maxEvents: 2,
            durationMs: 10_000);

        button.PerformClick();
        button.PerformClick();
        button.PerformClick();
        var page = registry.Read(trace.TraceId, afterSequence: 0, maxEvents: 10);

        Assert.Multiple(() => {
            Assert.That(page.Events, Has.Count.EqualTo(2));
            Assert.That(page.Events[0].Sequence, Is.EqualTo(2));
            Assert.That(page.Events[1].Sequence, Is.EqualTo(3));
            Assert.That(page.NextSequence, Is.EqualTo(3));
            Assert.That(page.DroppedEventCount, Is.EqualTo(1));
            Assert.That(page.Truncated, Is.True);
            Assert.That(page.Events.All(item => item.EventName == "Click"), Is.True);
        });

        var stopped = registry.Stop(trace.TraceId);
        button.PerformClick();
        Assert.That(stopped.Active, Is.False);
        Assert.Throws<InvalidOperationException>(() => registry.Read(trace.TraceId, 0, 10));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public async Task EventTrace_ExpiresAndDetachesHandlers() {
        using var button = new Button { Name = "expiringButton" };
        using var registry = new RuntimeEventTraceRegistry(new RuntimeBridgeOptions {
            MaxEventTraceEvents = 4,
            MaxEventTraceDurationMs = 50
        });
        var trace = registry.Start(
            [new RuntimeEventTraceRegistry.TraceControlTarget {
                Control = button,
                ControlId = "ctrl_2",
                ControlName = button.Name,
                ControlType = button.GetType().FullName!,
                ControlPath = "Form/expiringButton"
            }],
            ["Click"],
            maxEvents: 4,
            durationMs: 20);
        await Task.Delay(100);
        button.PerformClick();

        Assert.Throws<InvalidOperationException>(() => registry.Read(trace.TraceId, 0, 10));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void EventTrace_StopReleasesSubscribedControls() {
        using var registry = new RuntimeEventTraceRegistry(new RuntimeBridgeOptions());
        using var control = new Button { Name = "releasedButton" };
        var trace = registry.Start(
            [new RuntimeEventTraceRegistry.TraceControlTarget {
                Control = control,
                ControlId = "ctrl_3",
                ControlName = control.Name,
                ControlType = control.GetType().FullName!,
                ControlPath = "Form/releasedButton"
            }],
            ["Click"],
            maxEvents: 4,
            durationMs: 10_000);
        var stopped = registry.Stop(trace.TraceId);
        control.PerformClick();

        Assert.Multiple(() => {
            Assert.That(stopped.Active, Is.False);
            Assert.That(stopped.SubscribedControlCount, Is.EqualTo(1));
            Assert.That(stopped.Events, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => registry.Read(trace.TraceId, 0, 10));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void EventTrace_TruncatesLargeTextEvidence() {
        using var textBox = new TextBox { Name = "largeText" };
        using var registry = new RuntimeEventTraceRegistry(new RuntimeBridgeOptions());
        var trace = registry.Start(
            [new RuntimeEventTraceRegistry.TraceControlTarget {
                Control = textBox,
                ControlId = "ctrl_4",
                ControlName = textBox.Name,
                ControlType = textBox.GetType().FullName!,
                ControlPath = "Form/largeText"
            }],
            ["TextChanged"],
            maxEvents: 4,
            durationMs: 10_000);

        textBox.Text = new string('x', 5_000);
        var page = registry.Read(trace.TraceId, afterSequence: 0, maxEvents: 4);
        var state = page.Events.Single().Evidence["state"];

        Assert.Multiple(() => {
            Assert.That(state.GetProperty("text").GetString(), Has.Length.EqualTo(4_096));
            Assert.That(state.GetProperty("textTruncated").GetBoolean(), Is.True);
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void EventTrace_NextSequenceIsAReusableReadCursor() {
        using var button = new Button { Name = "pagedButton" };
        using var registry = new RuntimeEventTraceRegistry(new RuntimeBridgeOptions());
        var started = registry.Start(
            [new RuntimeEventTraceRegistry.TraceControlTarget {
                Control = button,
                ControlId = "ctrl_5",
                ControlName = button.Name,
                ControlType = button.GetType().FullName!,
                ControlPath = "Form/pagedButton"
            }],
            ["Click"],
            maxEvents: 8,
            durationMs: 10_000);
        Assert.That(started.NextSequence, Is.EqualTo(0));

        button.PerformClick();
        button.PerformClick();
        var first = registry.Read(started.TraceId, started.NextSequence, maxEvents: 1);
        var second = registry.Read(started.TraceId, first.NextSequence, maxEvents: 1);
        var empty = registry.Read(started.TraceId, second.NextSequence, maxEvents: 1);

        Assert.Multiple(() => {
            Assert.That(first.Events.Single().Sequence, Is.EqualTo(1));
            Assert.That(first.NextSequence, Is.EqualTo(1));
            Assert.That(first.Truncated, Is.True);
            Assert.That(second.Events.Single().Sequence, Is.EqualTo(2));
            Assert.That(second.NextSequence, Is.EqualTo(2));
            Assert.That(second.Truncated, Is.False);
            Assert.That(empty.Events, Is.Empty);
            Assert.That(empty.NextSequence, Is.EqualTo(2));
        });
    }

    private static DiagnosticControlRecord Record(
        string id,
        string? parentId,
        RectSnapshot bounds,
        SizeSnapshot parentClientSize) => new() {
            Summary = new ControlSummary {
                Identity = new ControlIdentity { ManagedId = id, Name = id, Type = "System.Windows.Forms.Button" },
                ParentId = parentId,
                Bounds = bounds,
                Visible = true,
                Enabled = true,
                Dock = "None",
                Anchor = "Top, Left"
            },
            State = new ControlStateSnapshot { Visible = true, Enabled = true },
            Layout = new ControlLayoutSnapshot {
                Bounds = bounds,
                ClientSize = new SizeSnapshot { Width = bounds.Width, Height = bounds.Height },
                ParentClientSize = parentId is null ? null : parentClientSize,
                DeviceDpi = 96,
                ScaleFactor = 1
            },
            IsContainer = false
        };

    private static byte[] CreatePng(Color background, params (int X, int Y, Color Color)[] pixels) {
        using var bitmap = new Bitmap(2, 2);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.Clear(background);
        foreach (var (x, y, color) in pixels)
            bitmap.SetPixel(x, y, color);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}