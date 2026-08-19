using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection;

/// <summary>
/// Reads the small, stable part of third-party layered window state that is
/// useful to an inspector. This class deliberately has no compile-time
/// dependency on AntdUI: provider detection is based on type identity and a
/// narrowly controlled reflection allow-list.
/// </summary>
internal static class LayeredWindowInspector {
    private const int MaxNestedDepth = 2;

    private static readonly string[] ConfigMembers = ["config", "Config"];
    private static readonly string[] OwnerMembers = ["PARENT", "Owner", "Form", "Target", "Content"];

    public static IReadOnlyDictionary<IntPtr, ProviderWindowMetadataSnapshot> InspectOpenForms(
        int processId,
        ControlIdentityRegistry identityRegistry,
        Func<Control, string> getControlPath,
        int maxItems) {
        var result = new Dictionary<IntPtr, ProviderWindowMetadataSnapshot>();
        if (processId != Process.GetCurrentProcess().Id || maxItems < 0)
            return result;

        Form[] forms;
        try {
            forms = Application.OpenForms.Cast<Form>().ToArray();
        }
        catch {
            return result;
        }

        foreach (var form in forms) {
            if (form is null || form.IsDisposed || form.Disposing || !IsSupportedLayeredForm(form.GetType()))
                continue;

            try {
                if (!form.IsHandleCreated || form.Handle == IntPtr.Zero)
                    continue;

                var metadata = CreateMetadata(form, identityRegistry, getControlPath, maxItems);
                result[form.Handle] = metadata;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or TargetInvocationException) {
                // A popup can be disposed between OpenForms enumeration and
                // snapshot creation. The HWND tree should remain available.
            }
        }

        return result;
    }

    internal static string ClassifySemanticType(string runtimeTypeName) {
        var name = runtimeTypeName.ToLowerInvariant();
        if (Has(name, "selectdown") || Has(name, "selectmultiple"))
            return "select-dropdown";
        if (Has(name, "menudown"))
            return "menu-popup";
        if (Has(name, "contextmenu"))
            return "context-menu";
        if (Has(name, "datepicker"))
            return "date-picker";
        if (Has(name, "timepicker"))
            return "time-picker";
        if (Has(name, "tooltip"))
            return "tooltip";
        if (Has(name, "modal"))
            return "modal";
        if (Has(name, "drawer"))
            return "drawer";
        if (Has(name, "message"))
            return "message";
        if (Has(name, "notification"))
            return "notification";
        if (Has(name, "dropdown"))
            return "dropdown";
        if (Has(name, "popover"))
            return "popover";
        if (Has(name, "tour"))
            return "tour";
        if (Has(name, "colorpicker"))
            return "color-picker";
        return "layered-window";
    }

    private static ProviderWindowMetadataSnapshot CreateMetadata(
        Form form,
        ControlIdentityRegistry identityRegistry,
        Func<Control, string> getControlPath,
        int maxItems) {
        var type = form.GetType();
        var runtimeType = type.FullName ?? type.Name;
        var semanticType = ClassifySemanticType(runtimeType);
        var metadata = new ProviderWindowMetadataSnapshot {
            Provider = "AntdUI",
            RuntimeWindowType = runtimeType,
            SemanticType = semanticType,
            Hwnd = Win32WindowInspector.FormatHandle(form.Handle),
            Visible = SafeGet(() => form.Visible, false),
            Dpi = ReadDouble(form, "Dpi")
        };

        try {
            if (form.IsHandleCreated)
                metadata.ContentBounds = ToRect(form.RectangleToScreen(form.ClientRectangle));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) {
            metadata.Warnings.Add($"Content bounds were unavailable: {ex.Message}");
        }

        var targetRect = ReadRectangle(form, "TargetRect");
        if (targetRect.HasValue)
            metadata.TargetBounds = ToRect(targetRect.Value);

        var owner = FindOwner(form);
        if (owner is not null && !owner.IsDisposed) {
            try {
                metadata.OwnerControlId = identityRegistry.GetOrCreateId(owner);
                metadata.OwnerControlPath = getControlPath(owner);
                metadata.OwnerControlName = SafeGet(() => owner.Name, null);
                metadata.OwnerControlType = owner.GetType().FullName ?? owner.GetType().Name;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) {
                metadata.Warnings.Add($"Owner control was disposed during inspection: {ex.Message}");
            }
        }

        if (IsCollectionWindow(semanticType))
            ReadItems(form, owner, metadata, maxItems);

        return metadata;
    }

    private static bool IsSupportedLayeredForm(Type type) {
        var fullName = type.FullName ?? type.Name;
        var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        var antAssembly = assemblyName.IndexOf("AntdUI", StringComparison.OrdinalIgnoreCase) >= 0;
        var antNamespace = fullName.StartsWith("AntdUI.", StringComparison.Ordinal);
        if (!antAssembly && !antNamespace)
            return false;

        var name = type.Name;
        return Has(fullName, "ILayeredForm") ||
            Has(name, "LayeredForm") ||
            name is "TooltipForm" or "MessageFrm" or "NotificationFrm";
    }

    private static bool IsCollectionWindow(string semanticType) =>
        semanticType is "select-dropdown" or "menu-popup" or "context-menu" or "dropdown";

    private static Control? FindOwner(Form form) {
        var direct = ReadMember(form, "PARENT") as Control;
        if (direct is not null)
            return direct;

        try {
            if (form.Owner is not null)
                return form.Owner;
        }
        catch (ObjectDisposedException) {
            return null;
        }

        foreach (var configName in ConfigMembers) {
            var config = ReadMember(form, configName);
            if (config is null)
                continue;

            foreach (var memberName in OwnerMembers) {
                var candidate = ReadMember(config, memberName);
                var owner = ExtractControl(candidate);
                if (owner is not null)
                    return owner;
            }
        }

        return null;
    }

    private static Control? ExtractControl(object? candidate) {
        if (candidate is Control control)
            return control;
        if (candidate is null)
            return null;

        // AntdUI.Target exposes Value as a public property. Reading this
        // single known member covers modal/message/notification configs while
        // avoiding arbitrary method invocation or graph traversal.
        var value = ReadMember(candidate, "Value");
        return value as Control;
    }

    private static void ReadItems(
        Form form,
        Control? owner,
        ProviderWindowMetadataSnapshot metadata,
        int maxItems) {
        var rawItems = ReadMember(form, "Items");
        if (rawItems is not IEnumerable items || rawItems is string)
            return;

        var totalCount = TryGetCount(items);
        var selectedIndex = owner is null ? null : ReadInt(owner, "SelectedIndex");
        var hoveredIndex = ReadInt(form, "hoveindex");
        var budget = new ItemBudget(Math.Max(0, maxItems));
        var sourceIndex = 0;
        var visibleIndexes = new List<int>();

        try {
            foreach (var item in items) {
                if (item is null) {
                    sourceIndex++;
                    continue;
                }

                if (!budget.TryTake()) {
                    metadata.Truncated = true;
                    break;
                }

                try {
                    var snapshot = BuildItem(
                        item,
                        sourceIndex,
                        selectedIndex,
                        hoveredIndex,
                        depth: 0,
                        budget,
                        visibleIndexes);
                    metadata.Items.Add(snapshot);
                    if (snapshot.Selected == true && metadata.SelectedItem is null)
                        metadata.SelectedItem = snapshot;
                    if (snapshot.Highlighted == true && metadata.HighlightedItem is null)
                        metadata.HighlightedItem = snapshot;
                    if (snapshot.Visible == true)
                        visibleIndexes.Add(snapshot.Index);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or TargetInvocationException) {
                    metadata.Warnings.Add($"Popup item {sourceIndex} could not be read: {ex.Message}");
                }

                sourceIndex++;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or TargetInvocationException) {
            metadata.Truncated = true;
            metadata.Warnings.Add($"Popup items changed during inspection: {ex.Message}");
        }

        if (totalCount.HasValue && totalCount.Value > metadata.Items.Count)
            metadata.Truncated = true;

        if (visibleIndexes.Count > 0) {
            var first = visibleIndexes.Min();
            var last = visibleIndexes.Max();
            metadata.VisibleRange = new ProviderWindowRangeSnapshot {
                Start = first,
                Count = last - first + 1,
                TotalCount = totalCount
            };
        }
    }

    private static ProviderWindowItemSnapshot BuildItem(
        object item,
        int sourceIndex,
        int? selectedIndex,
        int? hoveredIndex,
        int depth,
        ItemBudget budget,
        List<int> visibleIndexes) {
        var model = ReadMember(item, "Val") ?? item;
        var logicalIndex = ReadInt(item, "I") ?? sourceIndex;
        var selected = selectedIndex.HasValue && logicalIndex == selectedIndex.Value;
        selected |= ReadBool(model, "Selected") == true;
        var highlighted = hoveredIndex.HasValue && sourceIndex == hoveredIndex.Value;
        highlighted |= ReadBool(item, "Hover") == true || ReadBool(model, "Hover") == true;
        var bounds = ReadRectangle(item, "Rect");
        var visible = ReadBool(item, "SID") ?? (bounds.HasValue && bounds.Value.Width > 0 && bounds.Value.Height > 0);

        var snapshot = new ProviderWindowItemSnapshot {
            Index = logicalIndex,
            Kind = model.GetType().Name,
            Name = ReadString(model, "Name") ?? ReadString(model, "ID") ?? ReadString(model, "Id"),
            Text = ReadString(model, "Text") ?? ReadString(item, "Text"),
            Value = ReadScalarString(model, "Value") ?? ReadScalarString(model, "Tag"),
            Enabled = ReadBool(model, "Enabled") ?? ReadBool(model, "Enable"),
            Selected = selected,
            Highlighted = highlighted,
            Visible = visible,
            Bounds = bounds.HasValue ? ToRect(bounds.Value) : null
        };

        if (depth < MaxNestedDepth) {
            var children = ReadMember(model, "Sub") as IEnumerable;
            if (children is not null && children is not string) {
                var childIndex = 0;
                foreach (var child in children) {
                    if (child is null)
                        continue;
                    if (!budget.TryTake())
                        break;
                    snapshot.Children.Add(BuildItem(
                        child,
                        childIndex++,
                        selectedIndex: null,
                        hoveredIndex: null,
                        depth + 1,
                        budget,
                        visibleIndexes));
                }
            }
        }

        return snapshot;
    }

    private static object? ReadMember(object target, string name) {
        try {
            for (var type = target.GetType(); type is not null; type = type.BaseType) {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field is not null)
                    return field.GetValue(target);

                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property is not null && property.GetIndexParameters().Length == 0 && property.GetMethod is not null)
                    return property.GetValue(target, null);
            }
        }
        catch (Exception ex) when (ex is MemberAccessException or TargetInvocationException or InvalidOperationException or ObjectDisposedException) {
            return null;
        }

        return null;
    }

    private static string? ReadString(object target, string name) {
        var value = ReadMember(target, name);
        return value as string;
    }

    private static string? ReadScalarString(object target, string name) {
        var value = ReadMember(target, name);
        if (value is null)
            return null;
        if (value is string text)
            return text;
        if (value is Enum || value is IFormattable)
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        return null;
    }

    private static bool? ReadBool(object target, string name) {
        var value = ReadMember(target, name);
        return value is bool boolean ? boolean : null;
    }

    private static int? ReadInt(object target, string name) {
        var value = ReadMember(target, name);
        return value switch {
            int number => number,
            short number => number,
            byte number => number,
            _ => null
        };
    }

    private static double? ReadDouble(object target, string name) {
        var value = ReadMember(target, name);
        return value switch {
            double number => number,
            float number => number,
            decimal number => (double)number,
            int number => number,
            _ => null
        };
    }

    private static Rectangle? ReadRectangle(object target, string name) {
        var value = ReadMember(target, name);
        return value is Rectangle rectangle ? rectangle : null;
    }

    private static int? TryGetCount(IEnumerable items) {
        return items switch {
            ICollection collection => collection.Count,
            _ => null
        };
    }

    private static bool Has(string value, string substring) =>
        value.IndexOf(substring, StringComparison.Ordinal) >= 0;

    private static T SafeGet<T>(Func<T> getter, T fallback) {
        try {
            return getter();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) {
            return fallback;
        }
    }

    private static RectSnapshot ToRect(Rectangle rectangle) => new() {
        X = rectangle.X,
        Y = rectangle.Y,
        Width = Math.Max(0, rectangle.Width),
        Height = Math.Max(0, rectangle.Height)
    };

    private sealed class ItemBudget {
        private int _remaining;

        public ItemBudget(int maximum) => _remaining = maximum;

        public bool TryTake() {
            if (_remaining <= 0)
                return false;
            _remaining--;
            return true;
        }
    }
}