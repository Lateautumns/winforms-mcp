using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Diagnostics;

internal sealed class DiagnosticControlRecord {
    public ControlSummary Summary { get; set; } = new();
    public ControlStateSnapshot State { get; set; } = new();
    public ControlLayoutSnapshot Layout { get; set; } = new();
    public List<ControlBindingSnapshot> Bindings { get; set; } = new();
    public bool TabStop { get; set; }
    public bool ParentAutoScroll { get; set; }
    public bool IsContainer { get; set; }
    public SizeSnapshot? MeasuredText { get; set; }
    public SizeSnapshot? AvailableText { get; set; }
}