using System.Text.Json;
using System.Windows.Forms;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

internal sealed class ControlProviderContext {
    public ControlProviderContext(
        int maxDepth,
        int maxNodes,
        Func<Control, string> getControlId,
        Func<object?, JsonElement> toJsonValue,
        int? start = null,
        int? count = null,
        int? startRow = null,
        int? rowCount = null,
        string? rowScope = null) {
        MaxDepth = Math.Max(0, maxDepth);
        MaxNodes = Math.Max(1, maxNodes);
        GetControlId = getControlId;
        ToJsonValue = toJsonValue;
        Start = Math.Max(0, start ?? 0);
        Count = count.HasValue ? Math.Max(0, count.Value) : null;
        StartRow = Math.Max(0, startRow ?? 0);
        RowCount = rowCount.HasValue ? Math.Max(0, rowCount.Value) : null;
        RowScope = string.IsNullOrWhiteSpace(rowScope) ? null : rowScope;
    }

    public int MaxDepth { get; }

    public int MaxNodes { get; }

    /// <summary>
    /// Zero-based offset for a bounded collection such as tab pages or root items.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Optional maximum number of items to return from <see cref="Start"/>.
    /// </summary>
    public int? Count { get; }

    /// <summary>
    /// Zero-based row offset for table semantics.
    /// </summary>
    public int StartRow { get; }

    /// <summary>
    /// Optional maximum number of table rows to return from <see cref="StartRow"/>.
    /// </summary>
    public int? RowCount { get; }

    /// <summary>
    /// Optional requested table scope (data, visible, or rendered).
    /// </summary>
    public string? RowScope { get; }

    public Func<Control, string> GetControlId { get; }

    public Func<object?, JsonElement> ToJsonValue { get; }
}