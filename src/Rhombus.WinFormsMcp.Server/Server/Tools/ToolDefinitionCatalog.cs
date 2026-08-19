using System.Text.Json;

using ModelContextProtocol.Protocol;

namespace Rhombus.WinFormsMcp.Server.Tools;

internal static class ToolDefinitionCatalog {
    public static IList<Tool> All { get; } = [
        Define(ToolNames.FindElement, "Find one UI element and cache it for later calls.", Props(
            ("automationId", String("AutomationId to match")), ("name", String("Accessible name to match")),
            ("className", String("Window class name to match")), ("controlType", String("UIA control type to match")),
            ("parent", String("Optional cached parent element ID")))),
        Define(ToolNames.FindElements, "Find all UI elements matching the supplied criteria.", Props(
            ("automationId", String("AutomationId to match")), ("name", String("Accessible name to match")),
            ("className", String("Window class name to match")), ("controlType", String("UIA control type to match")),
            ("parent", String("Optional cached parent element ID")))),
        Define(ToolNames.ClickElement, "Click a cached UI element.", Props(
            ("elementId", String("Cached element ID")), ("doubleClick", Boolean("Perform a double click"))), "elementId"),
        Define(ToolNames.TypeText, "Type text using keyboard simulation on the visible desktop.", Props(
            ("elementId", String("Cached element ID")), ("text", String("Text to type")),
            ("clearFirst", Boolean("Clear existing text first"))), "elementId", "text"),
        Define(ToolNames.SetValue, "Set text through UIA ValuePattern; works on hidden desktops.", Props(
            ("elementId", String("Cached element ID")), ("value", String("Value to set"))), "elementId", "value"),
        Define(ToolNames.GetProperty, "Read a UIA property or pattern value from a cached element.", Props(
            ("elementId", String("Cached element ID")), ("propertyName", String("Property name"))), "elementId", "propertyName"),
        Define(ToolNames.LaunchApp, "Launch a WinForms application and return its process ID.", Props(
            ("path", String("Executable path")), ("arguments", String("Optional command-line arguments")),
            ("workingDirectory", String("Optional working directory"))), "path"),
        Define(ToolNames.AttachToProcess, "Attach to a running process by PID or process name.", Props(
            ("pid", Integer("Process ID")), ("processName", String("Process name without extension")))),
        Define(ToolNames.CloseApp, "Close an application gracefully or forcefully.", Props(
            ("pid", Integer("Process ID")), ("force", Boolean("Kill instead of sending a close request"))), "pid"),
        Define(ToolNames.GetProcessStatus, "Return process state, exit code, responsiveness, title, and stderr.", Props(
            ("pid", Integer("Process ID"))), "pid"),
        Define(ToolNames.TakeScreenshot, "Capture a process window or cached element and return the PNG as base64 image content.", Props(
            ("pid", Integer("Optional process ID")), ("elementId", String("Optional cached element ID")),
            ("outputPath", String("Optional path where the PNG is also saved")))),
        Define(ToolNames.ElementExists, "Check whether an element exists by AutomationId.", Props(
            ("automationId", String("AutomationId to find"))), "automationId"),
        Define(ToolNames.WaitForElement, "Wait for an element to appear by AutomationId.", Props(
            ("automationId", String("AutomationId to wait for")), ("timeoutMs", Integer("Maximum wait in milliseconds"))), "automationId"),
        Define(ToolNames.DragDrop, "Drag one cached element onto another on the visible desktop.", Props(
            ("sourceElementId", String("Cached source element ID")), ("targetElementId", String("Cached target element ID"))),
            "sourceElementId", "targetElementId"),
        Define(ToolNames.SendKeys, "Send keyboard input using SendKeys syntax.", Props(
            ("keys", String("SendKeys expression")), ("pid", Integer("Optional process to focus"))), "keys"),
        Define(ToolNames.SelectItem, "Select an item by text or zero-based index.", Props(
            ("elementId", String("Cached selection control ID")), ("value", String("Item text")),
            ("index", Integer("Zero-based item index"))), "elementId"),
        Define(ToolNames.ClickMenuItem, "Navigate and click a menu item by path.", Props(
            ("menuPath", Array("Menu labels from root to target", "string")), ("pid", Integer("Optional process ID"))), "menuPath"),
        Define(ToolNames.RenderForm, "Render a WinForms Designer file to a PNG without building the target project.", Props(
            ("designerFilePath", String("Designer.cs or companion .cs path")),
            ("outputPath", String("Optional path where the PNG is also saved")),
            ("theme", String("Optional visual theme: Light, Dark, or Auto")),
            ("dpi", Integer("Optional render DPI: 96, 120, 144, or 192")),
            ("providerProfile", String("Optional provider profile: AntdUI or StandardWinForms"))), "designerFilePath"),
        Define(ToolNames.GetElementTree, "Return a bounded UI Automation tree and cache every returned element.", Props(
            ("pid", Integer("Process whose main window is the root")), ("elementId", String("Optional cached root element ID")),
            ("depth", Integer("Maximum traversal depth")), ("maxElements", Integer("Maximum returned elements")))),
        Define(ToolNames.WaitForCondition, "Wait for an element property to satisfy a comparison.", Props(
            ("elementId", String("Cached element ID")), ("propertyName", String("Property to poll")),
            ("expectedValue", String("Expected value")), ("comparison", String("equals, contains, not_equals, greater_than, or less_than")),
            ("timeoutMs", Integer("Maximum wait in milliseconds"))), "elementId", "propertyName", "expectedValue"),
        Define(ToolNames.ToggleElement, "Toggle a checkbox, radio button, or toggle control.", Props(
            ("elementId", String("Cached toggle element ID")), ("desiredState", String("on, off, or indeterminate"))), "elementId"),
        Define(ToolNames.ScrollElement, "Scroll a control through UIA ScrollPattern.", Props(
            ("elementId", String("Cached scrollable element ID")), ("direction", String("up, down, left, or right")),
            ("amount", Integer("Number of units")), ("scrollType", String("line or page"))), "elementId", "direction"),
        Define(ToolNames.GetTableData, "Read paged data from a grid or table.", Props(
            ("elementId", String("Cached grid element ID")), ("startRow", Integer("First row index")),
            ("rowCount", Integer("Maximum row count")), ("columns", Array("Optional column indices", "integer"))), "elementId"),
        Define(ToolNames.SetTableCell, "Set a grid cell value.", Props(
            ("elementId", String("Cached grid element ID")), ("row", Integer("Zero-based row")),
            ("column", Integer("Zero-based column")), ("value", String("New value"))), "elementId", "row", "column", "value"),
        Define(ToolNames.ManageWindow, "Minimize, maximize, restore, move, resize, show, hide, or focus a window.", Props(
            ("pid", Integer("Process ID")), ("action", String("Window action")), ("width", Integer("Optional width")),
            ("height", Integer("Optional height")), ("x", Integer("Optional X coordinate")), ("y", Integer("Optional Y coordinate"))), "pid", "action"),
        Define(ToolNames.ListWindows, "List top-level and owned windows for a process.", Props(
            ("pid", Integer("Process ID"))), "pid"),
        Define(ToolNames.GetFocusedElement, "Return and cache the currently focused UIA element.", Props(
            ("pid", Integer("Optional process filter")))),
        Define(ToolNames.RaiseEvent, "Invoke a supported action on a cached element.", Props(
            ("elementId", String("Cached element ID")), ("eventName", String("Event or action name"))), "elementId", "eventName"),
        Define(ToolNames.ListenForEvent, "Wait for a UI Automation event.", Props(
            ("elementId", String("Optional cached element ID")), ("eventType", String("UIA event type")),
            ("timeoutMs", Integer("Maximum wait in milliseconds"))), "eventType"),
        Define(ToolNames.OpenContextMenu, "Open a context menu and cache its root element.", Props(
            ("elementId", String("Cached target element ID"))), "elementId"),
        Define(ToolNames.GetClipboard, "Read text from the Windows clipboard.", Props()),
        Define(ToolNames.SetClipboard, "Write text to the Windows clipboard.", Props(
            ("text", String("Clipboard text"))), "text"),
        Define(ToolNames.ReadTooltip, "Read tooltip text associated with a cached element.", Props(
            ("elementId", String("Cached element ID"))), "elementId"),
        Define(ToolNames.RuntimeStatus, "Check whether a target process exposes the read-only WinForms RuntimeBridge.", Props(
            ("pid", Integer("Target process ID"))), "pid"),
        Define(ToolNames.GetControlTree, "Return a bounded managed Control.Controls tree from a target WinForms process.", Props(
            ("pid", Integer("Target process ID")), ("rootId", String("Optional managed control ID")),
            ("maxDepth", Integer("Maximum tree depth")), ("maxNodes", Integer("Maximum returned nodes")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid"),
        Define(ToolNames.InspectControl, "Inspect one managed WinForms control's identity, state, safe properties, layout, and optional provider semantics.", Props(
            ("pid", Integer("Target process ID")), ("controlId", String("Managed control ID")),
            ("sections", Array("identity, state, properties, layout, bindings, provider, or semantic", "string")),
            ("includeProperties", Array("Additional safe property names to read", "string")),
            ("maxDepth", Integer("Maximum provider semantic hierarchy depth")),
            ("maxNodes", Integer("Maximum provider semantic nodes")),
            ("start", Integer("Zero-based offset for top-level semantic collections")),
            ("count", Integer("Maximum top-level semantic collection items")),
            ("startRow", Integer("Zero-based AntdUI table row offset")),
            ("rowCount", Integer("Maximum AntdUI table rows")),
            ("rowScope", String("AntdUI table row scope: data, visible, or rendered")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid", "controlId"),
        Define(ToolNames.GetAncestors, "Return the managed parent chain for a control, nearest parent first.", Props(
            ("pid", Integer("Target process ID")), ("controlId", String("Managed control ID")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid", "controlId"),
        Define(ToolNames.GetWindowTree, "Return the bounded HWND tree for a target process, including dialogs and owned/pop-up windows.", Props(
            ("pid", Integer("Target process ID")), ("maxNodes", Integer("Maximum returned HWND nodes")),
            ("maxItems", Integer("Maximum provider popup items per window")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid"),
        Define(ToolNames.GetBindings, "Read DataBindings attached to one managed WinForms control.", Props(
            ("pid", Integer("Target process ID")), ("controlId", String("Managed control ID")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid", "controlId"),
        Define(ToolNames.GetSourceMapping, "Map a managed control to its Designer declaration, initialization, and event handler symbols.", Props(
            ("pid", Integer("Target process ID")), ("controlId", String("Managed control ID")),
            ("sourceRoot", String("Optional source or solution root to scan")),
            ("maxFiles", Integer("Maximum source files to scan and index")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid", "controlId"),
        Define(ToolNames.DetectLayoutIssues, "Detect bounded, evidence-based WinForms layout, DPI, and binding issues.", Props(
            ("pid", Integer("Target process ID")), ("rootId", String("Optional managed root control ID")),
            ("checks", Array("Checks: layout, dpi, or bindings", "string")),
            ("maxDepth", Integer("Maximum managed tree depth")), ("maxNodes", Integer("Maximum controls to scan")),
            ("maxDiagnostics", Integer("Maximum diagnostics to return")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid"),
        Define(ToolNames.CompareScreenshot, "Compare two PNG screenshots with a deterministic bounded pixel diff.", Props(
            ("beforePath", String("Path to the before PNG")), ("afterPath", String("Path to the after PNG")),
            ("beforeBase64", String("Optional before PNG as base64")), ("afterBase64", String("Optional after PNG as base64")),
            ("maxRegions", Integer("Maximum changed regions to return")),
            ("pixelThreshold", Integer("Per-channel difference threshold from 0 to 255")))),
        Define(ToolNames.CheckAccessibility, "Check bounded WinForms accessibility metadata and UIA patterns.", Props(
            ("pid", Integer("Target process ID")), ("rootId", String("Optional managed root control ID")),
            ("maxDepth", Integer("Maximum managed tree depth")), ("maxNodes", Integer("Maximum controls to inspect")),
            ("maxDiagnostics", Integer("Maximum diagnostics to return")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid"),
        Define(ToolNames.StartEventTrace, "Start a bounded read-only RuntimeBridge trace for whitelisted WinForms events.", Props(
            ("pid", Integer("Target process ID")), ("rootId", String("Optional managed root control ID")),
            ("events", Array("Whitelisted events such as Click, TextChanged, and FormClosing", "string")),
            ("maxEvents", Integer("Ring buffer capacity")), ("durationMs", Integer("Trace lifetime in milliseconds")),
            ("maxNodes", Integer("Maximum controls to subscribe")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid"),
        Define(ToolNames.ReadEventTrace, "Read new events from a bounded RuntimeBridge event trace.", Props(
            ("pid", Integer("Target process ID")), ("traceId", String("Trace session ID")),
            ("afterSequence", Integer("Return events after this sequence number; use the previous response nextSequence cursor")),
            ("maxEvents", Integer("Maximum events to return")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid", "traceId"),
        Define(ToolNames.StopEventTrace, "Stop a RuntimeBridge event trace and detach all event handlers.", Props(
            ("pid", Integer("Target process ID")), ("traceId", String("Trace session ID")),
            ("bridgeInstanceId", BridgeInstanceId())), "pid", "traceId")
    ];

    private static Tool Define(
        string name,
        string description,
        Dictionary<string, object> properties,
        params string[] required) {
        var schema = new Dictionary<string, object> {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false
        };
        if (required.Length > 0)
            schema["required"] = required;

        return new Tool {
            Name = name,
            Description = description,
            InputSchema = JsonSerializer.SerializeToElement(schema)
        };
    }

    private static Dictionary<string, object> Props(params (string Name, object Schema)[] properties) =>
        properties.ToDictionary(property => property.Name, property => property.Schema, StringComparer.Ordinal);

    private static object String(string description) => new { type = "string", description };
    private static object Integer(string description) => new { type = "integer", description };
    private static object Boolean(string description) => new { type = "boolean", description };
    private static object BridgeInstanceId() => String(
        "Optional RuntimeBridge instance ID from runtime status or a managed identity; rejects stale references after bridge restart");
    private static object Array(string description, string itemType) => new {
        type = "array",
        description,
        items = new { type = itemType }
    };
}