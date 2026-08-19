using System.Text.Json;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Diagnostics;

internal static class ControlDiagnosticRules {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DefaultChecks = ["layout", "dpi", "bindings"];

    public static RuntimeDiagnosticsSnapshot Analyze(
        IReadOnlyList<DiagnosticControlRecord> controls,
        IReadOnlyCollection<string>? requestedChecks,
        int maxNodes,
        int maxDiagnostics,
        bool traversalTruncated,
        CancellationToken cancellationToken,
        int processId = 0,
        string? bridgeInstanceId = null) {
        var checks = NormalizeChecks(requestedChecks);
        var result = new RuntimeDiagnosticsSnapshot {
            ProcessId = processId,
            BridgeInstanceId = bridgeInstanceId,
            Checks = checks.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            ScannedNodes = controls.Count,
            MaxNodes = maxNodes,
            MaxDiagnostics = maxDiagnostics,
            Truncated = traversalTruncated
        };
        var byId = controls.ToDictionary(
            control => control.Summary.Identity.ManagedId,
            StringComparer.Ordinal);

        foreach (var control in controls) {
            cancellationToken.ThrowIfCancellationRequested();
            if (checks.Contains("layout"))
                AnalyzeLayout(control, byId, result);
            if (checks.Contains("dpi"))
                AnalyzeDpi(control, byId, result);
            if (checks.Contains("bindings"))
                AnalyzeBindings(control, result);
            if (result.Diagnostics.Count >= maxDiagnostics) {
                result.Truncated = true;
                return result;
            }
        }

        if (checks.Contains("layout"))
            AnalyzeSiblingLayout(controls, result, cancellationToken);
        return result;
    }

    private static HashSet<string> NormalizeChecks(IReadOnlyCollection<string>? requestedChecks) {
        var checks = requestedChecks is { Count: > 0 }
            ? requestedChecks
            : DefaultChecks;
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var check in checks) {
            if (check is not null &&
                (check.Equals("layout", StringComparison.OrdinalIgnoreCase) ||
                 check.Equals("dpi", StringComparison.OrdinalIgnoreCase) ||
                 check.Equals("bindings", StringComparison.OrdinalIgnoreCase))) {
                result.Add(check.ToLowerInvariant());
            }
        }

        if (result.Count == 0)
            throw new ArgumentException("'checks' must contain layout, dpi, or bindings.");
        return result;
    }

    private static void AnalyzeLayout(
        DiagnosticControlRecord control,
        IReadOnlyDictionary<string, DiagnosticControlRecord> byId,
        RuntimeDiagnosticsSnapshot result) {
        var summary = control.Summary;
        var bounds = summary.Bounds;
        if (summary.Visible && (bounds.Width <= 0 || bounds.Height <= 0)) {
            Add(result, "warning", "invalid_control_size", summary.Identity.ManagedId,
                "Visible control has a zero or negative layout dimension.",
                ("bounds", bounds), ("visible", summary.Visible));
        }

        var minimum = control.Layout.MinimumSize;
        if ((minimum.Width > 0 && bounds.Width < minimum.Width) ||
            (minimum.Height > 0 && bounds.Height < minimum.Height)) {
            Add(result, "warning", "below_minimum_size", summary.Identity.ManagedId,
                "Control bounds are smaller than its configured MinimumSize.",
                ("bounds", bounds), ("minimumSize", minimum));
        }

        if (control.MeasuredText is not null && control.AvailableText is not null &&
            (control.MeasuredText.Width > control.AvailableText.Width + 2 ||
             control.MeasuredText.Height > control.AvailableText.Height + 2)) {
            Add(result, "warning", "text_clipped", summary.Identity.ManagedId,
                "Measured text exceeds the control's available client area.",
                ("text", summary.Text), ("measuredSize", control.MeasuredText),
                ("availableSize", control.AvailableText), ("deviceDpi", control.Layout.DeviceDpi));
        }

        if (!summary.Visible && summary.Enabled && control.TabStop) {
            Add(result, "info", "hidden_tab_stop", summary.Identity.ManagedId,
                "Control is hidden while still configured as a keyboard tab stop.",
                ("visible", false), ("enabled", true), ("tabStop", true));
        }

        if (summary.ParentId is null || control.ParentAutoScroll ||
            !byId.TryGetValue(summary.ParentId, out var parent) || !summary.Visible)
            return;

        var parentSize = parent.Layout.ClientSize;
        var outside = bounds.X < 0 || bounds.Y < 0 ||
            bounds.X + bounds.Width > parentSize.Width ||
            bounds.Y + bounds.Height > parentSize.Height;
        if (!outside)
            return;

        var anchorRelated = summary.Dock.Equals("None", StringComparison.OrdinalIgnoreCase) &&
            (summary.Anchor.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 ||
             summary.Anchor.IndexOf("Bottom", StringComparison.OrdinalIgnoreCase) >= 0);
        Add(result, "warning", anchorRelated ? "anchor_outside_parent" : "outside_parent",
            summary.Identity.ManagedId,
            anchorRelated
                ? "Anchored control extends outside its parent's client area."
                : "Control bounds extend outside its parent's client area.",
            ("bounds", bounds), ("parentClientSize", parentSize),
            ("anchor", summary.Anchor), ("dock", summary.Dock));
    }

    private static void AnalyzeDpi(
        DiagnosticControlRecord control,
        IReadOnlyDictionary<string, DiagnosticControlRecord> byId,
        RuntimeDiagnosticsSnapshot result) {
        var layout = control.Layout;
        var expectedScale = layout.DeviceDpi / 96d;
        if (layout.DeviceDpi <= 0 || Math.Abs(layout.ScaleFactor - expectedScale) > 0.01d) {
            Add(result, "warning", "invalid_dpi_scale", control.Summary.Identity.ManagedId,
                "Control DPI metadata is internally inconsistent.",
                ("deviceDpi", layout.DeviceDpi), ("scaleFactor", layout.ScaleFactor),
                ("expectedScaleFactor", expectedScale));
        }

        if (control.Summary.ParentId is not null &&
            byId.TryGetValue(control.Summary.ParentId, out var parent) &&
            layout.DeviceDpi != parent.Layout.DeviceDpi) {
            Add(result, "warning", "dpi_mismatch", control.Summary.Identity.ManagedId,
                "Control and parent report different device DPI values.",
                ("deviceDpi", layout.DeviceDpi), ("parentDeviceDpi", parent.Layout.DeviceDpi),
                ("parentControlId", parent.Summary.Identity.ManagedId));
        }
    }

    private static void AnalyzeBindings(
        DiagnosticControlRecord control,
        RuntimeDiagnosticsSnapshot result) {
        foreach (var binding in control.Bindings) {
            var evidence = new (string, object?)[] {
                ("property", binding.Property),
                ("dataMember", binding.DataMember),
                ("dataSourceType", binding.DataSourceType),
                ("dataSourceUpdateMode", binding.DataSourceUpdateMode),
                ("controlUpdateMode", binding.ControlUpdateMode)
            };
            if (!string.IsNullOrWhiteSpace(binding.Error)) {
                Add(result, "warning", "invalid_binding_metadata", control.Summary.Identity.ManagedId,
                    "Binding metadata could not be read completely.",
                    evidence.Append(("error", binding.Error)).ToArray());
            }
            if (binding.DataSourcePresent == false) {
                Add(result, "warning", "binding_data_source_missing", control.Summary.Identity.ManagedId,
                    "Binding does not have a data source.", evidence);
            }
            if (binding.ControlPropertyExists == false) {
                Add(result, "warning", "binding_control_property_missing", control.Summary.Identity.ManagedId,
                    "Binding targets a control property that is not present.", evidence);
            }
            if (binding.DataMemberExists == false) {
                Add(result, "warning", "binding_data_member_missing", control.Summary.Identity.ManagedId,
                    "Binding data member was not found in the available data source metadata.", evidence);
            }
            if (binding.ControlPropertyReadOnly == true &&
                !binding.DataSourceUpdateMode.Equals("Never", StringComparison.OrdinalIgnoreCase)) {
                Add(result, "warning", "binding_update_mode_mismatch", control.Summary.Identity.ManagedId,
                    "Binding attempts to update the data source from a read-only control property.", evidence);
            }
        }
    }

    private static void AnalyzeSiblingLayout(
        IReadOnlyList<DiagnosticControlRecord> controls,
        RuntimeDiagnosticsSnapshot result,
        CancellationToken cancellationToken) {
        foreach (var siblingGroup in controls
                     .Where(control => control.Summary.ParentId is not null)
                     .GroupBy(control => control.Summary.ParentId!, StringComparer.Ordinal)) {
            cancellationToken.ThrowIfCancellationRequested();
            var siblings = siblingGroup.Where(control => control.Summary.Visible).ToArray();
            var fillControls = siblings
                .Where(control => control.Summary.Dock.Equals("Fill", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (fillControls.Length > 1) {
                foreach (var control in fillControls) {
                    Add(result, "info", "multiple_dock_fill", control.Summary.Identity.ManagedId,
                        "Multiple visible siblings use DockStyle.Fill; z-order determines which surface is exposed.",
                        ("parentControlId", siblingGroup.Key),
                        ("fillControlIds", fillControls.Select(item => item.Summary.Identity.ManagedId).ToArray()));
                    if (AtLimit(result)) {
                        result.Truncated = true;
                        return;
                    }
                }
            }

            var overlapCandidates = siblings
                .Where(control => control.TabStop && !control.IsContainer &&
                                  control.Summary.Dock.Equals("None", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            for (var leftIndex = 0; leftIndex < overlapCandidates.Length; leftIndex++) {
                cancellationToken.ThrowIfCancellationRequested();
                for (var rightIndex = leftIndex + 1; rightIndex < overlapCandidates.Length; rightIndex++) {
                    if ((rightIndex & 63) == 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    var left = overlapCandidates[leftIndex];
                    var right = overlapCandidates[rightIndex];
                    var intersection = Intersect(left.Summary.Bounds, right.Summary.Bounds);
                    if (intersection.Width <= 2 || intersection.Height <= 2)
                        continue;

                    Add(result, "warning", "control_overlap", left.Summary.Identity.ManagedId,
                        "Two visible focusable sibling controls overlap.",
                        ("relatedControlId", right.Summary.Identity.ManagedId),
                        ("intersection", intersection),
                        ("controlBounds", left.Summary.Bounds),
                        ("relatedBounds", right.Summary.Bounds));
                    if (AtLimit(result)) {
                        result.Truncated = true;
                        return;
                    }
                }
            }
        }
    }

    private static RectSnapshot Intersect(RectSnapshot left, RectSnapshot right) {
        var x = Math.Max(left.X, right.X);
        var y = Math.Max(left.Y, right.Y);
        var rightEdge = Math.Min(left.X + left.Width, right.X + right.Width);
        var bottomEdge = Math.Min(left.Y + left.Height, right.Y + right.Height);
        return new RectSnapshot {
            X = x,
            Y = y,
            Width = Math.Max(0, rightEdge - x),
            Height = Math.Max(0, bottomEdge - y)
        };
    }

    private static void Add(
        RuntimeDiagnosticsSnapshot result,
        string severity,
        string code,
        string controlId,
        string message,
        params (string Name, object? Value)[] evidence) {
        if (AtLimit(result)) {
            result.Truncated = true;
            return;
        }

        var item = new DiagnosticSnapshot {
            Severity = severity,
            Code = code,
            ControlId = controlId,
            Message = message
        };
        foreach (var (name, value) in evidence)
            item.Evidence[name] = JsonSerializer.SerializeToElement(value, SerializerOptions);
        result.Diagnostics.Add(item);
    }

    private static bool AtLimit(RuntimeDiagnosticsSnapshot result) =>
        result.Diagnostics.Count >= result.MaxDiagnostics;
}