using System.Text.Json;

namespace Rhombus.WinFormsMcp.RuntimeContracts;

public sealed class DiagnosticSnapshot {
    public string Severity { get; set; } = "warning";
    public string Code { get; set; } = string.Empty;
    public string ControlId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> Evidence { get; set; } = new(StringComparer.Ordinal);
}

public sealed class RuntimeDiagnosticsSnapshot {
    public List<DiagnosticSnapshot> Diagnostics { get; set; } = new();
    public List<string> Checks { get; set; } = new();
    public int ScannedNodes { get; set; }
    public int MaxNodes { get; set; }
    public int MaxDiagnostics { get; set; }
    public bool Truncated { get; set; }
}

public sealed class AccessibilityControlSnapshot {
    public ControlSummary Summary { get; set; } = new();
    public string? AccessibleName { get; set; }
    public string? AccessibleDescription { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; }
    public bool Focused { get; set; }
    public bool Enabled { get; set; }
    public bool Visible { get; set; }
    public string? AutomationId { get; set; }
    public string? UiaControlType { get; set; }
    public List<string> UiaPatterns { get; set; } = new();
    public string? UiaCorrelationMethod { get; set; }
    public double? UiaCorrelationConfidence { get; set; }
}

public sealed class RuntimeAccessibilitySnapshot {
    public List<AccessibilityControlSnapshot> Controls { get; set; } = new();
    public List<DiagnosticSnapshot> Diagnostics { get; set; } = new();
    public int ScannedNodes { get; set; }
    public int MaxNodes { get; set; }
    public int MaxDiagnostics { get; set; }
    public bool Truncated { get; set; }
}

public sealed class RuntimeEventSnapshot {
    public long Sequence { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string ControlType { get; set; } = string.Empty;
    public string ControlPath { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> Evidence { get; set; } = new(StringComparer.Ordinal);
}

public sealed class RuntimeEventTraceSnapshot {
    public string TraceId { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int MaxEvents { get; set; }
    public int SubscribedControlCount { get; set; }
    public List<string> SubscribedEvents { get; set; } = new();
    public List<RuntimeEventSnapshot> Events { get; set; } = new();
    /// <summary>Pass this value as afterSequence to continue reading without gaps.</summary>
    public long NextSequence { get; set; }
    public long DroppedEventCount { get; set; }
    public bool Truncated { get; set; }
}