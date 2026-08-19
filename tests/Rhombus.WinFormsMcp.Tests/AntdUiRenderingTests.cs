using System.Drawing;
using System.Reflection;

using Rhombus.WinFormsMcp.Rendering;

namespace Rhombus.WinFormsMcp.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class AntdUiRenderingTests {
    private const string DesignerCode = """
        namespace Test {
            partial class AntdMatrixForm {
                private void InitializeComponent() {
                    this.button = new AntdUI.Button();
                    this.input = new AntdUI.Input();
                    this.tabs = new AntdUI.Tabs();
                    this.tree = new AntdUI.Tree();
                    this.table = new AntdUI.Table();
                    this.button.Location = new System.Drawing.Point(16, 16);
                    this.button.Size = new System.Drawing.Size(150, 42);
                    this.button.Text = "Deploy";
                    this.input.Location = new System.Drawing.Point(182, 16);
                    this.input.Size = new System.Drawing.Size(240, 42);
                    this.input.Text = "Device A";
                    this.tabs.Location = new System.Drawing.Point(16, 76);
                    this.tabs.Size = new System.Drawing.Size(406, 84);
                    this.tree.Location = new System.Drawing.Point(16, 178);
                    this.tree.Size = new System.Drawing.Size(196, 180);
                    this.table.Location = new System.Drawing.Point(228, 178);
                    this.table.Size = new System.Drawing.Size(394, 180);
                    this.ClientSize = new System.Drawing.Size(640, 380);
                    this.Controls.Add(this.button);
                    this.Controls.Add(this.input);
                    this.Controls.Add(this.tabs);
                    this.Controls.Add(this.tree);
                    this.Controls.Add(this.table);
                }
                private AntdUI.Button button;
                private AntdUI.Input input;
                private AntdUI.Tabs tabs;
                private AntdUI.Tree tree;
                private AntdUI.Table table;
            }
        }
        """;

    [Test]
    [Category("E2E")]
    public void RenderDesignerCode_AntdUiThemeDpiMatrix_ProducesIsolatedImages() {
        var antDuiPath = RequireAntdUiAssembly();
        var renderer = new DesignSurfaceFormRenderer();
        var results = new Dictionary<(string Theme, int Dpi), (byte[] Bytes, Size Size)>();

        foreach (var dpi in new[] { 96, 120, 144, 192 }) {
            foreach (var theme in new[] { "Light", "Dark" }) {
                var bytes = renderer.RenderDesignerCode(
                    DesignerCode,
                    extraAssemblyPaths: [antDuiPath],
                    theme: theme,
                    dpi: dpi,
                    providerProfile: "AntdUI");
                AssertValidPng(bytes);
                results[(theme, dpi)] = (bytes, ReadSize(bytes));
            }
        }

        Assert.Multiple(() => {
            foreach (var dpi in new[] { 96, 120, 144 })
                Assert.That(results[("Dark", dpi)].Bytes, Is.Not.EqualTo(results[("Light", dpi)].Bytes));

            Assert.That(results[("Light", 120)].Size.Width, Is.GreaterThan(results[("Light", 96)].Size.Width));
            Assert.That(results[("Light", 144)].Size.Width, Is.GreaterThan(results[("Light", 120)].Size.Width));
            Assert.That(results[("Light", 192)].Size.Width, Is.GreaterThan(results[("Light", 144)].Size.Width));
        });
    }

    [Test]
    public void RenderDesignerCode_AntdUiState_IsRestoredAfterSuccessAndFailure() {
        var antDuiPath = RequireAntdUiAssembly();
        var assembly = Assembly.LoadFrom(antDuiPath);
        var configType = assembly.GetType("AntdUI.Config", throwOnError: true)!;
        var modeProperty = configType.GetProperty("Mode", BindingFlags.Public | BindingFlags.Static)!;
        var customDpiField = configType.GetField("_dpi_custom", BindingFlags.NonPublic | BindingFlags.Static)!;
        var setDpi = configType.GetMethod("SetDpi", BindingFlags.Public | BindingFlags.Static)!;
        var originalMode = modeProperty.GetValue(null);
        var originalDpi = customDpiField.GetValue(null);

        try {
            var dark = Enum.Parse(modeProperty.PropertyType, "Dark");
            modeProperty.SetValue(null, dark);
            setDpi.Invoke(null, [(float?)1.5F]);

            var renderer = new DesignSurfaceFormRenderer();
            _ = renderer.RenderDesignerCode(
                DesignerCode,
                extraAssemblyPaths: [antDuiPath],
                theme: "Light",
                dpi: 120,
                providerProfile: "AntdUI");
            AssertState(modeProperty, customDpiField, dark, 1.5F);

            Assert.Throws<InvalidOperationException>(() => renderer.RenderDesignerCode(
                "namespace Test { class MissingDesigner { } }",
                extraAssemblyPaths: [antDuiPath],
                theme: "Light",
                dpi: 96,
                providerProfile: "AntdUI"));
            AssertState(modeProperty, customDpiField, dark, 1.5F);
        }
        finally {
            setDpi.Invoke(null, [originalDpi]);
            modeProperty.SetValue(null, originalMode);
        }
    }

    private static void AssertState(
        PropertyInfo modeProperty,
        FieldInfo customDpiField,
        object expectedMode,
        float expectedDpi) {
        Assert.Multiple(() => {
            Assert.That(modeProperty.GetValue(null), Is.EqualTo(expectedMode));
            Assert.That(customDpiField.GetValue(null), Is.EqualTo(expectedDpi));
        });
    }

    private static string RequireAntdUiAssembly() {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "AntdUI.dll");
        if (!File.Exists(path))
            Assert.Ignore("AntdUI.dll was not copied to the test output.");
        return path;
    }

    private static Size ReadSize(byte[] pngBytes) {
        using var stream = new MemoryStream(pngBytes);
        using var bitmap = new Bitmap(stream);
        return bitmap.Size;
    }

    private static void AssertValidPng(byte[] bytes) {
        Assert.That(bytes.Length, Is.GreaterThan(8));
        Assert.That(bytes.Take(4), Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
    }
}