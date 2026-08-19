using System.Text.Json;

using FlaUI.Core.AutomationElements;

using Rhombus.WinFormsMcp.RuntimeContracts;
using Rhombus.WinFormsMcp.Server.Runtime;
using Rhombus.WinFormsMcp.Server.Tools.Runtime;

namespace Rhombus.WinFormsMcp.Server.Tools.Diagnostics;

internal sealed class CheckAccessibilityToolHandler : IToolHandler {
    private readonly IRuntimeBridgeClient _client;
    private readonly ManagedUiaCorrelationService _correlation;

    public CheckAccessibilityToolHandler(
        IRuntimeBridgeClient client,
        ManagedUiaCorrelationService correlation) {
        _client = client;
        _correlation = correlation;
    }

    public string Name => ToolNames.CheckAccessibility;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var pid = RuntimeToolSupport.RequirePid(arguments);
        var maxNodes = Math.Clamp(ToolArguments.GetInt32(arguments, "maxNodes", 100), 1, 100);
        var maxDiagnostics = Math.Clamp(ToolArguments.GetInt32(arguments, "maxDiagnostics", 200), 1, 2_000);
        try {
            var result = await _client.GetAccessibilityAsync(
                pid,
                ToolArguments.GetString(arguments, "rootId"),
                ToolArguments.GetInt32(arguments, "maxDepth", 6),
                maxNodes,
                maxDiagnostics,
                cancellationToken,
                RuntimeToolSupport.GetBridgeInstanceId(arguments)).ConfigureAwait(false);
            foreach (var control in result.Controls) {
                cancellationToken.ThrowIfCancellationRequested();
                EnrichUia(control);
                AddUiaDiagnostics(result, control, maxDiagnostics);
            }
            return ToolJson.Result(new { success = true, accessibility = result });
        }
        catch (RuntimeBridgeException ex) {
            throw RuntimeToolSupport.ToToolException(ex);
        }
    }

    private void EnrichUia(AccessibilityControlSnapshot control) {
        try {
            var correlation = _correlation.TryResolve(control.Summary.Identity, lookupTimeoutMs: 100);
            if (correlation is null)
                return;
            control.UiaCorrelationMethod = correlation.Method;
            control.UiaCorrelationConfidence = correlation.Confidence;
            var element = correlation.Element;
            control.UiaControlType = element.ControlType.ToString();
            control.UiaPatterns = GetSupportedPatterns(element);
        }
        catch {
            // UIA is an optional enrichment; managed accessibility data remains valid.
        }
    }

    private static void AddUiaDiagnostics(
        RuntimeAccessibilitySnapshot result,
        AccessibilityControlSnapshot control,
        int maxDiagnostics) {
        if (result.Diagnostics.Count >= maxDiagnostics) {
            result.Truncated = true;
            return;
        }

        if (control.Visible && control.Enabled && control.TabStop && string.IsNullOrWhiteSpace(control.AutomationId)) {
            AddDiagnostic(result, "warning", "missing_automation_id", control,
                "Visible enabled control has no stable AutomationId.",
                ("controlType", control.Summary.Identity.Type));
        }
        if (control.Visible && control.Enabled && string.IsNullOrWhiteSpace(control.UiaControlType)) {
            AddDiagnostic(result, "info", "uia_not_correlated", control,
                "Managed control could not be correlated to a UI Automation element.",
                ("automationId", control.AutomationId), ("hwnd", control.Summary.Identity.Hwnd));
            return;
        }

        var expectedPatterns = control.UiaControlType switch {
            "Button" => new[] { "Invoke" },
            "CheckBox" or "RadioButton" => new[] { "Toggle", "SelectionItem" },
            "Edit" => new[] { "Value" },
            "ComboBox" => new[] { "ExpandCollapse", "Selection" },
            "List" or "ListItem" => new[] { "Selection", "SelectionItem" },
            _ => Array.Empty<string>()
        };
        if (expectedPatterns.Length > 0 &&
            !expectedPatterns.Any(expected => control.UiaPatterns.Contains(expected, StringComparer.Ordinal))) {
            AddDiagnostic(result, "warning", "uia_action_pattern_missing", control,
                "UI Automation element does not expose an expected interaction pattern for its control type.",
                ("uiaControlType", control.UiaControlType),
                ("expectedPatterns", expectedPatterns),
                ("supportedPatterns", control.UiaPatterns));
        }
    }

    private static void AddDiagnostic(
        RuntimeAccessibilitySnapshot result,
        string severity,
        string code,
        AccessibilityControlSnapshot control,
        string message,
        params (string Name, object? Value)[] evidence) {
        if (result.Diagnostics.Count >= result.MaxDiagnostics) {
            result.Truncated = true;
            return;
        }
        var diagnostic = new DiagnosticSnapshot {
            Severity = severity,
            Code = code,
            ControlId = control.Summary.Identity.ManagedId,
            Message = message
        };
        foreach (var (name, value) in evidence)
            diagnostic.Evidence[name] = JsonSerializer.SerializeToElement(value);
        result.Diagnostics.Add(diagnostic);
    }

    private static List<string> GetSupportedPatterns(AutomationElement element) {
        var result = new List<string>();
        TryAdd(result, "Invoke", () => element.Patterns.Invoke.IsSupported);
        TryAdd(result, "Value", () => element.Patterns.Value.IsSupported);
        TryAdd(result, "Toggle", () => element.Patterns.Toggle.IsSupported);
        TryAdd(result, "Selection", () => element.Patterns.Selection.IsSupported);
        TryAdd(result, "SelectionItem", () => element.Patterns.SelectionItem.IsSupported);
        TryAdd(result, "ExpandCollapse", () => element.Patterns.ExpandCollapse.IsSupported);
        TryAdd(result, "RangeValue", () => element.Patterns.RangeValue.IsSupported);
        TryAdd(result, "Scroll", () => element.Patterns.Scroll.IsSupported);
        TryAdd(result, "Grid", () => element.Patterns.Grid.IsSupported);
        TryAdd(result, "Table", () => element.Patterns.Table.IsSupported);
        TryAdd(result, "Window", () => element.Patterns.Window.IsSupported);
        return result;
    }

    private static void TryAdd(List<string> result, string name, Func<bool> isSupported) {
        try {
            if (isSupported())
                result.Add(name);
        }
        catch {
            // Individual provider pattern failures must not fail the diagnostic tool.
        }
    }
}