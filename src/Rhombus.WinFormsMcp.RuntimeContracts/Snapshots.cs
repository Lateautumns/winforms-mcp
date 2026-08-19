using System.Text.Json;
using System.Text.Json.Serialization;

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
    public bool? DataSourcePresent { get; set; }
    public bool? DataMemberExists { get; set; }
    public bool? ControlPropertyExists { get; set; }
    public bool? ControlPropertyReadOnly { get; set; }
    public string? Error { get; set; }
}

public sealed class ControlProviderSnapshot {
    public string ProviderName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string RuntimeType { get; set; } = string.Empty;
    public string SemanticType { get; set; } = string.Empty;
    public string? ProviderVersion { get; set; }
    public List<string> Capabilities { get; set; } = new();
}

/// <summary>
/// Optional bounded-read settings for provider-specific semantic inspection.
/// Values are clamped by the RuntimeBridge before a provider receives them.
/// </summary>
public sealed class ControlSemanticOptions {
    /// <summary>
    /// Maximum hierarchy depth for semantic children.
    /// </summary>
    public int? MaxDepth { get; set; }

    /// <summary>
    /// Maximum semantic nodes returned by a provider.
    /// </summary>
    public int? MaxNodes { get; set; }

    /// <summary>
    /// Zero-based offset for top-level semantic collections.
    /// </summary>
    public int? Start { get; set; }

    /// <summary>
    /// Maximum top-level semantic collection items to return.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// Zero-based table row offset.
    /// </summary>
    public int? StartRow { get; set; }

    /// <summary>
    /// Maximum table rows to return.
    /// </summary>
    public int? RowCount { get; set; }

    /// <summary>
    /// Requested AntdUI table scope: data, visible, or rendered.
    /// </summary>
    public string? RowScope { get; set; }
}

public sealed class SemanticNodeSnapshot {
    public string Kind { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Text { get; set; }
    public string? Value { get; set; }
    public int? Index { get; set; }
    public string? ControlId { get; set; }
    public Dictionary<string, JsonElement> State { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, JsonElement> Properties { get; set; } = new(StringComparer.Ordinal);
    public List<SemanticNodeSnapshot> Children { get; set; } = new();
    public int ChildCount { get; set; }
    public bool Truncated { get; set; }
}

public sealed class ControlSemanticSnapshot {
    public string ProviderName { get; set; } = string.Empty;
    public string RuntimeType { get; set; } = string.Empty;
    public string SemanticType { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> State { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, JsonElement> Properties { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Errors { get; set; } = new(StringComparer.Ordinal);
    public List<string> SupportedInteractionHints { get; set; } = new();
    public List<SemanticNodeSnapshot> Children { get; set; } = new();
    public int ChildCount { get; set; }
    public bool Truncated { get; set; }
    public Dictionary<string, JsonElement> Metadata { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ControlInspectionSnapshot {
    public ControlSummary Summary { get; set; } = new();
    public ControlStateSnapshot State { get; set; } = new();
    public ControlPropertiesSnapshot Properties { get; set; } = new();
    public ControlLayoutSnapshot Layout { get; set; } = new();
    public List<ControlBindingSnapshot> Bindings { get; set; } = new();
    public ControlProviderSnapshot? Provider { get; set; }
    public ControlSemanticSnapshot? Semantic { get; set; }
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
    /// <summary>
    /// Optional metadata supplied by a UI framework provider for transient or
    /// layered windows. The base HWND snapshot remains useful when no provider
    /// is present.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProviderWindowMetadataSnapshot? ProviderWindowMetadata { get; set; }
}

/// <summary>
/// Bounded, read-only metadata for a provider-owned window such as an AntdUI
/// dropdown, tooltip, drawer, or notification.
/// </summary>
public sealed class ProviderWindowMetadataSnapshot {
    public string Provider { get; set; } = string.Empty;
    public string RuntimeWindowType { get; set; } = string.Empty;
    public string SemanticType { get; set; } = string.Empty;
    public string Hwnd { get; set; } = string.Empty;
    public string? OwnerControlId { get; set; }
    public string? OwnerControlPath { get; set; }
    public string? OwnerControlName { get; set; }
    public string? OwnerControlType { get; set; }
    public RectSnapshot Bounds { get; set; } = new();
    public RectSnapshot? ContentBounds { get; set; }
    public RectSnapshot? TargetBounds { get; set; }
    public bool Visible { get; set; }
    public double? Dpi { get; set; }
    public List<ProviderWindowItemSnapshot> Items { get; set; } = new();
    public ProviderWindowItemSnapshot? SelectedItem { get; set; }
    public ProviderWindowItemSnapshot? HighlightedItem { get; set; }
    public ProviderWindowRangeSnapshot? VisibleRange { get; set; }
    public bool Truncated { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// A safe summary of one item rendered by a provider popup. Values are kept as
/// strings so arbitrary third-party objects never cross the process boundary.
/// </summary>
public sealed class ProviderWindowItemSnapshot {
    public int Index { get; set; }
    public string? Kind { get; set; }
    public string? Name { get; set; }
    public string? Text { get; set; }
    public string? Value { get; set; }
    public bool? Enabled { get; set; }
    public bool? Selected { get; set; }
    public bool? Highlighted { get; set; }
    public bool? Visible { get; set; }
    public RectSnapshot? Bounds { get; set; }
    public List<ProviderWindowItemSnapshot> Children { get; set; } = new();
}

public sealed class ProviderWindowRangeSnapshot {
    public int Start { get; set; }
    public int Count { get; set; }
    public int? TotalCount { get; set; }
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
    /// <summary>
    /// Read-only metadata describing the bounded source-index refresh used for this mapping.
    /// This is optional so existing consumers can continue to deserialize the original contract.
    /// </summary>
    public SourceIndexSnapshot? Index { get; set; }
}

public sealed class SourceIndexSnapshot {
    public string Root { get; set; } = string.Empty;
    public int MaxFiles { get; set; }
    public int Scanned { get; set; }
    public int Parsed { get; set; }
    public int Reused { get; set; }
    public int Removed { get; set; }
    public int CachedFiles { get; set; }
    public bool Truncated { get; set; }
    public int ParseErrors { get; set; }
    public List<string> Warnings { get; set; } = new();
}