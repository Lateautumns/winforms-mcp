using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

namespace Rhombus.WinFormsMcp.Tests.Runtime;

[TestFixture]
public sealed class AntdUIProviderTests {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Registry_PrefersAntdUIProviderOverStandardFallback() {
        using var button = new global::AntdUI.Button {
            Text = "Save"
        };
        var registry = ControlProviderRegistry.CreateDefault();

        var provider = registry.Resolve(button);

        Assert.That(provider.ProviderName, Is.EqualTo("AntdUI"));
        Assert.That(provider.Describe(button).SemanticType, Is.EqualTo("button"));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void AntdUIProvider_ReadsButtonInputAndSwitchSemantics() {
        var provider = new AntdUIProvider();
        using var button = new global::AntdUI.Button {
            Text = "Run",
            Loading = true,
            Toggle = true,
            IconSvg = "<svg />"
        };
        TrySetProperty(button, "Type", "Primary");
        TrySetProperty(button, "Shape", "Round");
        TrySetProperty(button, "Radius", 6);

        using var input = new global::AntdUI.Input {
            Text = "Device",
            PlaceholderText = "Search",
            PrefixText = "SN",
            SuffixSvg = "<svg />",
            ReadOnly = true,
            Multiline = false
        };
        TrySetProperty(input, "Status", "Success");

        using var toggle = new global::AntdUI.Switch {
            Checked = true,
            Loading = true
        };
        var context = CreateContext();

        var buttonSemantic = provider.Inspect(button, context);
        var inputSemantic = provider.Inspect(input, context);
        var switchSemantic = provider.Inspect(toggle, context);

        Assert.Multiple(() => {
            Assert.That(buttonSemantic.SemanticType, Is.EqualTo("button"));
            Assert.That(buttonSemantic.State["loading"].GetBoolean(), Is.True);
            Assert.That(buttonSemantic.State["toggle"].GetBoolean(), Is.True);
            Assert.That(buttonSemantic.Properties["type"].GetString(), Does.Contain("Primary"));
            Assert.That(buttonSemantic.Properties["shape"].GetString(), Does.Contain("Round"));
            Assert.That(buttonSemantic.Properties["iconSvg"].GetString(), Is.EqualTo("<svg />"));
            Assert.That(inputSemantic.SemanticType, Is.EqualTo("textbox"));
            Assert.That(inputSemantic.State["readOnly"].GetBoolean(), Is.True);
            Assert.That(inputSemantic.Properties["placeholderText"].GetString(), Is.EqualTo("Search"));
            Assert.That(inputSemantic.Properties["prefixText"].GetString(), Is.EqualTo("SN"));
            Assert.That(inputSemantic.Properties["suffixSvg"].GetString(), Is.EqualTo("<svg />"));
            Assert.That(inputSemantic.Properties["status"].GetString(), Does.Contain("Success"));
            Assert.That(switchSemantic.SemanticType, Is.EqualTo("switch"));
            Assert.That(switchSemantic.State["checked"].GetBoolean(), Is.True);
            Assert.That(switchSemantic.State["loading"].GetBoolean(), Is.True);
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void AntdUIProvider_ReturnsBoundedSelectItems() {
        var provider = new AntdUIProvider();
        using var select = new global::AntdUI.Select {
            Text = "Beta"
        };
        select.Items.Add(new global::AntdUI.SelectItem("Alpha", "A") {
            SubText = "First",
            IconSvg = "<svg-a />"
        });
        select.Items.Add(new global::AntdUI.SelectItem("Beta", "B") {
            SubText = "Second"
        });
        select.SelectedValue = "B";

        var semantic = provider.Inspect(
            select,
            new ControlProviderContext(
                maxDepth: 4,
                maxNodes: 1,
                getControlId: control => control.Name,
                toJsonValue: ToJsonValue));

        Assert.Multiple(() => {
            Assert.That(semantic.SemanticType, Is.EqualTo("select"));
            Assert.That(semantic.State["selectedIndex"].GetInt32(), Is.EqualTo(1));
            Assert.That(semantic.State["selectedValue"].GetString(), Is.EqualTo("B"));
            Assert.That(semantic.ChildCount, Is.EqualTo(2));
            Assert.That(semantic.Children, Has.Count.EqualTo(1));
            Assert.That(semantic.Children[0].Text, Is.EqualTo("Alpha"));
            Assert.That(semantic.Children[0].Value, Is.EqualTo("A"));
            Assert.That(semantic.Children[0].Properties["subText"].GetString(), Is.EqualTo("First"));
            Assert.That(semantic.Truncated, Is.True);
        });
    }

    private static ControlProviderContext CreateContext() =>
        new(
            maxDepth: 4,
            maxNodes: 20,
            getControlId: control => control.Name,
            toJsonValue: ToJsonValue);

    private static JsonElement ToJsonValue(object? value) =>
        JsonSerializer.SerializeToElement(value, SerializerOptions);

    private static void TrySetProperty(object target, string propertyName, object? value) {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
            return;

        var actualType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var converted = actualType.IsEnum && value is string text
            ? Enum.Parse(actualType, text, ignoreCase: true)
            : Convert.ChangeType(value, actualType, CultureInfo.InvariantCulture);
        property.SetValue(target, converted);
    }
}