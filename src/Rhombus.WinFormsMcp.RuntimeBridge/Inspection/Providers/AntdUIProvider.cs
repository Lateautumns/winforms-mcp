using System.Collections;
using System.Reflection;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

internal sealed class AntdUIProvider : IControlProvider {
    private const string NamespacePrefix = "AntdUI.";

    private static readonly IReadOnlyDictionary<string, ControlProfile> Profiles =
        new Dictionary<string, ControlProfile>(StringComparer.Ordinal) {
            ["AntdUI.Button"] = new(
                "button",
                ["invoke"],
                [
                    Read.State("Loading", "loading"),
                    Read.State("Toggle", "toggle")
                ],
                [
                    Read.Property("Type", "type"),
                    Read.Property("Shape", "shape"),
                    Read.Property("Radius", "radius"),
                    Read.Property("Ghost", "ghost"),
                    Read.Property("AutoToggle", "autoToggle"),
                    Read.Property("ToggleText", "toggleText"),
                    Read.Property("IconSvg", "iconSvg"),
                    Read.Property("LoadingSvg", "loadingSvg"),
                    Read.Property("LoadingRespondClick", "loadingRespondClick"),
                    Read.Property("DialogResult", "dialogResult")
                ]),
            ["AntdUI.Input"] = new(
                "textbox",
                ["text-input"],
                [
                    Read.State("ReadOnly", "readOnly")
                ],
                [
                    Read.Property("PlaceholderText", "placeholderText"),
                    Read.Property("PrefixText", "prefixText"),
                    Read.Property("PrefixSvg", "prefixSvg"),
                    Read.Property("SuffixText", "suffixText"),
                    Read.Property("SuffixSvg", "suffixSvg"),
                    Read.Property("Status", "status"),
                    Read.Property("Radius", "radius"),
                    Read.Property("Multiline", "multiline"),
                    Read.Property("AcceptsTab", "acceptsTab"),
                    Read.Property("AcceptsEscape", "acceptsEscape"),
                    Read.Property("HideSelection", "hideSelection"),
                    Read.Property("UseContextMenu", "useContextMenu"),
                    Read.Property("MaxLength", "maxLength"),
                    Read.Property("VirtualMode", "virtualMode")
                ]),
            ["AntdUI.InputNumber"] = new(
                "number-input",
                ["text-input", "numeric"],
                [
                    Read.State("ReadOnly", "readOnly"),
                    Read.State("Value", "value")
                ],
                [
                    Read.Property("Minimum", "minimum"),
                    Read.Property("Maximum", "maximum"),
                    Read.Property("Increment", "increment"),
                    Read.Property("AlwaysShowControl", "alwaysShowControl"),
                    Read.Property("InterceptArrowKeys", "interceptArrowKeys"),
                    Read.Property("EnabledValueTextChange", "enabledValueTextChange")
                ]),
            ["AntdUI.Checkbox"] = new(
                "checkbox",
                ["toggle"],
                [
                    Read.State("Checked", "checked")
                ],
                [
                    Read.Property("AutoCheck", "autoCheck")
                ]),
            ["AntdUI.Radio"] = new(
                "radio",
                ["select"],
                [
                    Read.State("Checked", "checked")
                ],
                [
                    Read.Property("AutoCheck", "autoCheck")
                ]),
            ["AntdUI.Switch"] = new(
                "switch",
                ["toggle"],
                [
                    Read.State("Checked", "checked"),
                    Read.State("Loading", "loading")
                ],
                [
                    Read.Property("AutoCheck", "autoCheck")
                ]),
            ["AntdUI.Select"] = new(
                "select",
                ["select"],
                [
                    Read.State("SelectedIndex", "selectedIndex"),
                    Read.State("SelectedValue", "selectedValue")
                ],
                [
                    Read.Property("List", "list"),
                    Read.Property("Placement", "placement"),
                    Read.Property("MaxCount", "maxCount"),
                    Read.Property("AutoPrefixSvg", "autoPrefixSvg")
                ],
                includeItems: true)
        };

    private static readonly Read[] CommonReads = [
        Read.Property("ColorScheme", "colorScheme"),
        Read.Property("Dpi", "dpi"),
        Read.Property("HandCursor", "handCursor")
    ];

    public string ProviderName => "AntdUI";

    public int Priority => 100;

    public bool CanHandle(Control control) => IsAntdUIControl(control.GetType());

    public ControlProviderSnapshot Describe(Control control) {
        var runtimeType = GetRuntimeType(control);
        var profile = ResolveProfile(control.GetType());
        return new ControlProviderSnapshot {
            ProviderName = ProviderName,
            Priority = Priority,
            RuntimeType = runtimeType,
            SemanticType = profile.SemanticType,
            ProviderVersion = control.GetType().Assembly.GetName().Version?.ToString(),
            Capabilities = [
                "antduiBasicSemantics",
                "identity",
                "state",
                "properties",
                "layout",
                "bindings"
            ]
        };
    }

    public ControlSemanticSnapshot Inspect(Control control, ControlProviderContext context) {
        var profile = ResolveProfile(control.GetType());
        var semantic = new ControlSemanticSnapshot {
            ProviderName = ProviderName,
            RuntimeType = GetRuntimeType(control),
            SemanticType = profile.SemanticType,
            ChildCount = TryGet(() => control.Controls.Count, 0),
            SupportedInteractionHints = profile.InteractionHints.ToList()
        };

        AddState(semantic, context, "text", TryGet(() => control.Text, string.Empty));
        AddState(semantic, context, "visible", TryGet(() => control.Visible, false));
        AddState(semantic, context, "enabled", TryGet(() => control.Enabled, false));
        AddState(semantic, context, "focused", TryGet(() => control.Focused, false));
        AddMetadata(semantic, context, "providerVersion", control.GetType().Assembly.GetName().Version?.ToString());

        foreach (var read in CommonReads.Concat(profile.Reads))
            AddRead(semantic, control, context, read);

        if (profile.IncludeItems)
            AddSelectItems(semantic, control, context);

        return semantic;
    }

    private static void AddSelectItems(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context) {
        if (!TryReadProperty(control, "Items", out var items, out var error)) {
            if (!string.IsNullOrEmpty(error))
                semantic.Errors["Items"] = error!;
            return;
        }

        if (items is not IEnumerable enumerable)
            return;

        var count = TryGetCollectionCount(items);
        if (count.HasValue)
            semantic.ChildCount = count.Value;

        var index = 0;
        foreach (var item in enumerable) {
            if (index >= context.MaxNodes) {
                semantic.Truncated = true;
                break;
            }

            semantic.Children.Add(BuildSelectItem(item, index, context));
            index++;
        }
    }

    private static SemanticNodeSnapshot BuildSelectItem(
        object? item,
        int index,
        ControlProviderContext context) {
        if (item is null) {
            return new SemanticNodeSnapshot {
                Kind = "select-item",
                Index = index
            };
        }

        var text = TryReadStringProperty(item, "Text") ?? item.ToString();
        var value = TryReadStringProperty(item, "Value") ??
            TryReadStringProperty(item, "Tag") ??
            TryReadStringProperty(item, "SubText");
        var node = new SemanticNodeSnapshot {
            Kind = "select-item",
            Text = text,
            Value = value,
            Index = index,
            Properties = {
                ["runtimeType"] = context.ToJsonValue(item.GetType().FullName)
            }
        };

        AddItemProperty(node, item, context, "SubText", "subText");
        AddItemProperty(node, item, context, "IconSvg", "iconSvg");
        AddItemProperty(node, item, context, "Online", "online");
        return node;
    }

    private static void AddItemProperty(
        SemanticNodeSnapshot node,
        object item,
        ControlProviderContext context,
        string propertyName,
        string key) {
        if (TryReadProperty(item, propertyName, out var value, out _) && value is not null)
            node.Properties[key] = context.ToJsonValue(SanitizeValue(value));
    }

    private static void AddRead(
        ControlSemanticSnapshot semantic,
        Control control,
        ControlProviderContext context,
        Read read) {
        if (!TryReadProperty(control, read.PropertyName, out var value, out var error)) {
            if (!string.IsNullOrEmpty(error))
                semantic.Errors[read.PropertyName] = error!;
            return;
        }

        var target = read.Target == ReadTarget.State ? semantic.State : semantic.Properties;
        target[read.Key] = context.ToJsonValue(SanitizeValue(value));
    }

    private static object? SanitizeValue(object? value) {
        if (value is null)
            return null;
        var type = value.GetType();
        if (type.IsPrimitive || value is string or decimal)
            return value;
        if (value is Enum)
            return value.ToString();
        if (value is IEnumerable && value is not string)
            return value.ToString();
        return value.ToString();
    }

    private static bool TryReadProperty(
        object target,
        string propertyName,
        out object? value,
        out string? error) {
        value = null;
        error = null;
        var property = FindPublicProperty(target.GetType(), propertyName);
        if (property is null)
            return false;
        if (property.GetIndexParameters().Length > 0)
            return false;

        try {
            value = property.GetValue(target);
            return true;
        }
        catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }

    private static PropertyInfo? FindPublicProperty(Type type, string propertyName) {
        for (var current = type; current is not null; current = current.BaseType) {
            var property = current.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (property is not null)
                return property;
        }

        return null;
    }

    private static string? TryReadStringProperty(object target, string propertyName) {
        if (!TryReadProperty(target, propertyName, out var value, out _) || value is null)
            return null;
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int? TryGetCollectionCount(object value) {
        if (value is ICollection collection)
            return collection.Count;
        return TryReadProperty(value, "Count", out var count, out _) && count is int typedCount
            ? typedCount
            : null;
    }

    private static void AddState(
        ControlSemanticSnapshot semantic,
        ControlProviderContext context,
        string name,
        object? value) =>
        semantic.State[name] = context.ToJsonValue(SanitizeValue(value));

    private static void AddMetadata(
        ControlSemanticSnapshot semantic,
        ControlProviderContext context,
        string name,
        object? value) =>
        semantic.Metadata[name] = context.ToJsonValue(SanitizeValue(value));

    private static string GetRuntimeType(Control control) =>
        control.GetType().FullName ?? control.GetType().Name;

    private static ControlProfile ResolveProfile(Type type) {
        for (var current = type; current is not null; current = current.BaseType) {
            var fullName = current.FullName;
            if (fullName is not null && Profiles.TryGetValue(fullName, out var profile))
                return profile;
        }

        return new ControlProfile("antdui-control", [], [], []);
    }

    private static bool IsAntdUIControl(Type type) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (current.FullName?.StartsWith(NamespacePrefix, StringComparison.Ordinal) == true)
                return true;
        }

        return false;
    }

    private static T TryGet<T>(Func<T> callback, T fallback) {
        try {
            return callback();
        }
        catch {
            return fallback;
        }
    }

    private enum ReadTarget {
        State,
        Property
    }

    private sealed class Read {
        private Read(string propertyName, string key, ReadTarget target) {
            PropertyName = propertyName;
            Key = key;
            Target = target;
        }

        public string PropertyName { get; }

        public string Key { get; }

        public ReadTarget Target { get; }

        public static Read State(string propertyName, string key) =>
            new(propertyName, key, ReadTarget.State);

        public static Read Property(string propertyName, string key) =>
            new(propertyName, key, ReadTarget.Property);
    }

    private sealed class ControlProfile {
        public ControlProfile(
            string semanticType,
            IReadOnlyList<string> interactionHints,
            IReadOnlyList<Read> stateReads,
            IReadOnlyList<Read> propertyReads,
            bool includeItems = false) {
            SemanticType = semanticType;
            InteractionHints = interactionHints;
            StateReads = stateReads;
            PropertyReads = propertyReads;
            IncludeItems = includeItems;
        }

        public string SemanticType { get; }

        public IReadOnlyList<string> InteractionHints { get; }

        public IReadOnlyList<Read> StateReads { get; }

        public IReadOnlyList<Read> PropertyReads { get; }

        public bool IncludeItems { get; }

        public IEnumerable<Read> Reads => StateReads.Concat(PropertyReads);
    }
}