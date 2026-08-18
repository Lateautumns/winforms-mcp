using System.Text.Json;

namespace Rhombus.WinFormsMcp.RuntimeContracts;

public sealed class PointSnapshot {
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class SizeSnapshot {
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class RectSnapshot {
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class ThicknessSnapshot {
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
}

public sealed class ControlIdentity {
    public string ManagedId { get; set; } = string.Empty;
    public string? UiaId { get; set; }
    public string? Hwnd { get; set; }
    public int ProcessId { get; set; }
    public string ControlPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    /// <summary>
    /// Fully qualified type of the owning WinForms Form. This is distinct from
    /// <see cref="Type"/>, which identifies the control itself.
    /// </summary>
    public string? OwnerType { get; set; }
    public string? AutomationId { get; set; }
}

public sealed class UiaCorrelation {
    public string? UiaId { get; set; }
    public string Method { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public sealed class ControlSummary {
    public ControlIdentity Identity { get; set; } = new();
    public string? Text { get; set; }
    public RectSnapshot Bounds { get; set; } = new();
    public bool Visible { get; set; }
    public bool Enabled { get; set; }
    public int ChildCount { get; set; }
    public string? ParentId { get; set; }
    public string Dock { get; set; } = string.Empty;
    public string Anchor { get; set; } = string.Empty;
}

public sealed class ControlTreeNode {
    public ControlSummary Summary { get; set; } = new();
    public List<ControlTreeNode> Children { get; set; } = new();
    public bool Truncated { get; set; }
}

public sealed class ControlTreeSnapshot {
    public List<ControlTreeNode> Roots { get; set; } = new();
    public int NodeCount { get; set; }
    public bool Truncated { get; set; }
    public int MaxDepth { get; set; }
    public int MaxNodes { get; set; }
}

public sealed class ControlStateSnapshot {
    public bool Visible { get; set; }
    public bool Enabled { get; set; }
    public bool Focused { get; set; }
    public bool ReadOnly { get; set; }
    public string? Text { get; set; }
}

public sealed class ControlLayoutSnapshot {
    public RectSnapshot Bounds { get; set; } = new();
    public RectSnapshot ScreenBounds { get; set; } = new();
    public RectSnapshot ClientRectangle { get; set; } = new();
    public string Dock { get; set; } = string.Empty;
    public string Anchor { get; set; } = string.Empty;
    public ThicknessSnapshot Margin { get; set; } = new();
    public ThicknessSnapshot Padding { get; set; } = new();
    public bool AutoSize { get; set; }
    public SizeSnapshot MinimumSize { get; set; } = new();
    public SizeSnapshot MaximumSize { get; set; } = new();
    public SizeSnapshot ClientSize { get; set; } = new();
    public SizeSnapshot? ParentClientSize { get; set; }
    public int DeviceDpi { get; set; }
    public double ScaleFactor { get; set; }
}

public sealed class ControlPropertiesSnapshot {
    public Dictionary<string, JsonElement> Values { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Errors { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ControlBindingSnapshot {
    public string Property { get; set; } = string.Empty;
    public string? DataMember { get; set; }
    public string? DataSourceType { get; set; }
    public bool FormattingEnabled { get; set; }
    public string DataSourceUpdateMode { get; set; } = string.Empty;
    public string ControlUpdateMode { get; set; } = string.Empty;
}

public sealed class ControlInspectionSnapshot {
    public ControlSummary Summary { get; set; } = new();
    public ControlStateSnapshot State { get; set; } = new();
    public ControlPropertiesSnapshot Properties { get; set; } = new();
    public ControlLayoutSnapshot Layout { get; set; } = new();
    public List<ControlBindingSnapshot> Bindings { get; set; } = new();
    public UiaCorrelation? Correlation { get; set; }
}

public sealed class ControlAncestorSnapshot {
    public string ManagedId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ControlPath { get; set; } = string.Empty;
    public int Depth { get; set; }
}

public sealed class WindowSnapshot {
    public string Hwnd { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public RectSnapshot Bounds { get; set; } = new();
    public bool Visible { get; set; }
    public bool Enabled { get; set; }
    public string? Owner { get; set; }
    public string? Parent { get; set; }
    public string Kind { get; set; } = "Window";
    public List<WindowSnapshot> Children { get; set; } = new();
}

public sealed class SourceLocationSnapshot {
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
}

public sealed class EventHandlerSnapshot {
    public string Event { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public string FullyQualifiedSymbol { get; set; } = string.Empty;
}

public sealed class SourceMappingSnapshot {
    public ControlIdentity Control { get; set; } = new();
    public SourceLocationSnapshot? Declaration { get; set; }
    public SourceLocationSnapshot? Initialization { get; set; }
    public SourceLocationSnapshot? Designer { get; set; }
    public string Namespace { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FullyQualifiedType { get; set; } = string.Empty;
    public string? CodeBehindFile { get; set; }
    public Dictionary<string, EventHandlerSnapshot> Events { get; set; } = new(StringComparer.Ordinal);
    public List<string> Warnings { get; set; } = new();
}
