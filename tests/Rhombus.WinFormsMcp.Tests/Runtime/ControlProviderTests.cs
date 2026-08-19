using System.Text.Json;
using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;
using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Tests.Runtime;

[TestFixture]
public sealed class ControlProviderTests {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Test]
    [Apartment(ApartmentState.STA)]
    public void ControlProviderRegistry_UsesHighestPriorityMatchingProvider() {
        using var button = new Button();
        var registry = new ControlProviderRegistry([
            new FakeProvider("low", 10, control => control is Button, "low-button"),
            new StandardWinFormsProvider(),
            new FakeProvider("high", 20, control => control is Button, "high-button")
        ]);

        var provider = registry.Resolve(button);

        Assert.That(provider.ProviderName, Is.EqualTo("high"));
        Assert.That(provider.Describe(button).SemanticType, Is.EqualTo("high-button"));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StandardWinFormsProvider_MapsCommonControls() {
        var provider = new StandardWinFormsProvider();
        using var form = new Form();
        using var button = new Button();
        using var textBox = new TextBox();
        using var comboBox = new ComboBox();
        using var listBox = new ListBox();
        using var treeView = new TreeView();
        using var grid = new DataGridView();
        using var tabs = new TabControl();
        using var menu = new MenuStrip();

        Assert.Multiple(() => {
            Assert.That(provider.Describe(form).SemanticType, Is.EqualTo("form"));
            Assert.That(provider.Describe(button).SemanticType, Is.EqualTo("button"));
            Assert.That(provider.Describe(textBox).SemanticType, Is.EqualTo("text-input"));
            Assert.That(provider.Describe(comboBox).SemanticType, Is.EqualTo("select"));
            Assert.That(provider.Describe(listBox).SemanticType, Is.EqualTo("list"));
            Assert.That(provider.Describe(treeView).SemanticType, Is.EqualTo("tree"));
            Assert.That(provider.Describe(grid).SemanticType, Is.EqualTo("table"));
            Assert.That(provider.Describe(tabs).SemanticType, Is.EqualTo("tabs"));
            Assert.That(provider.Describe(menu).SemanticType, Is.EqualTo("menu"));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void StandardWinFormsProvider_FallsBackForUnknownThirdPartyControl() {
        using var control = new UnknownThirdPartyControl {
            Name = "customWidget",
            Text = "Custom"
        };
        var provider = ControlProviderRegistry.CreateDefault().Resolve(control);
        var context = new ControlProviderContext(
            maxDepth: 4,
            maxNodes: 20,
            getControlId: item => item.Name,
            toJsonValue: ToJsonValue);

        var description = provider.Describe(control);
        var semantic = provider.Inspect(control, context);

        Assert.Multiple(() => {
            Assert.That(description.ProviderName, Is.EqualTo("StandardWinForms"));
            Assert.That(description.SemanticType, Is.EqualTo("custom-control"));
            Assert.That(semantic.ProviderName, Is.EqualTo("StandardWinForms"));
            Assert.That(semantic.SemanticType, Is.EqualTo("custom-control"));
            Assert.That(semantic.State["text"].GetString(), Is.EqualTo("Custom"));
            Assert.That(semantic.State["enabled"].GetBoolean(), Is.True);
        });
    }

    private static JsonElement ToJsonValue(object? value) =>
        JsonSerializer.SerializeToElement(value, SerializerOptions);

    private sealed class UnknownThirdPartyControl : Control {
    }

    private sealed class FakeProvider : IControlProvider {
        private readonly Func<Control, bool> _canHandle;
        private readonly string _semanticType;

        public FakeProvider(
            string providerName,
            int priority,
            Func<Control, bool> canHandle,
            string semanticType) {
            ProviderName = providerName;
            Priority = priority;
            _canHandle = canHandle;
            _semanticType = semanticType;
        }

        public string ProviderName { get; }

        public int Priority { get; }

        public bool CanHandle(Control control) => _canHandle(control);

        public ControlProviderSnapshot Describe(Control control) => new() {
            ProviderName = ProviderName,
            Priority = Priority,
            RuntimeType = control.GetType().FullName ?? control.GetType().Name,
            SemanticType = _semanticType
        };

        public ControlSemanticSnapshot Inspect(Control control, ControlProviderContext context) => new() {
            ProviderName = ProviderName,
            RuntimeType = control.GetType().FullName ?? control.GetType().Name,
            SemanticType = _semanticType
        };
    }
}