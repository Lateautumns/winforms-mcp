using System.Text.Json;
using System.Windows.Forms;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

internal sealed class ControlProviderContext {
    public ControlProviderContext(
        int maxDepth,
        int maxNodes,
        Func<Control, string> getControlId,
        Func<object?, JsonElement> toJsonValue) {
        MaxDepth = maxDepth;
        MaxNodes = maxNodes;
        GetControlId = getControlId;
        ToJsonValue = toJsonValue;
    }

    public int MaxDepth { get; }

    public int MaxNodes { get; }

    public Func<Control, string> GetControlId { get; }

    public Func<object?, JsonElement> ToJsonValue { get; }
}