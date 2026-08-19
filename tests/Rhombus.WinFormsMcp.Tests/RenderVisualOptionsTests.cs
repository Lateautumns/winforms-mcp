using System.Drawing;

using Rhombus.WinFormsMcp.Rendering;
using Rhombus.WinFormsMcp.Server.Tools;

namespace Rhombus.WinFormsMcp.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class RenderVisualOptionsTests {
    [TestCase("light", "Light")]
    [TestCase("DARK", "Dark")]
    [TestCase("Auto", "Auto")]
    public void Normalize_Theme_IsCaseInsensitive(string input, string expected) {
        var options = RenderVisualOptions.Normalize(input, 120, "antd-ui");

        Assert.Multiple(() => {
            Assert.That(options.Theme, Is.EqualTo(expected));
            Assert.That(options.Dpi, Is.EqualTo(120));
            Assert.That(options.ProviderProfile, Is.EqualTo("AntdUI"));
            Assert.That(options.ScaleFactor, Is.EqualTo(1.25F));
        });
    }

    [TestCase(96)]
    [TestCase(120)]
    [TestCase(144)]
    [TestCase(192)]
    public void Normalize_SupportedDpi_IsAccepted(int dpi) {
        Assert.That(RenderVisualOptions.Normalize(null, dpi, "WinForms").Dpi, Is.EqualTo(dpi));
    }

    [TestCase("sepia", null, "AntdUI", "render_invalid_theme")]
    [TestCase(null, 128, "AntdUI", "render_invalid_dpi")]
    [TestCase(null, null, "Unknown", "render_invalid_provider_profile")]
    [TestCase("Dark", 96, "StandardWinForms", "render_profile_unsupported")]
    public void Normalize_InvalidProfileValues_ReturnStructuredCode(
        string? theme,
        int? dpi,
        string provider,
        string expectedCode) {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            RenderVisualOptions.Normalize(theme, dpi, provider));

        Assert.That(FormRenderErrors.GetCode(exception!), Is.EqualTo(expectedCode));
    }

    [Test]
    public void BuildCacheKey_ThemeDpiAndProvider_AreIsolated() {
        var baseline = DesignSurfaceFormRenderer.BuildCacheKey("designer", "companion", null);
        var light96 = DesignSurfaceFormRenderer.BuildCacheKey(
            "designer", "companion", null, "Light", 96, "AntdUI");
        var dark96 = DesignSurfaceFormRenderer.BuildCacheKey(
            "designer", "companion", null, "Dark", 96, "AntdUI");
        var light144 = DesignSurfaceFormRenderer.BuildCacheKey(
            "designer", "companion", null, "Light", 144, "AntdUI");
        var standard144 = DesignSurfaceFormRenderer.BuildCacheKey(
            "designer", "companion", null, null, 144, "StandardWinForms");

        Assert.That(
            new[] { baseline, light96, dark96, light144, standard144 }.Distinct().Count(),
            Is.EqualTo(5));
    }

    [Test]
    public void RenderDesignerCode_DpiScalesStandardWinFormsOutput() {
        const string designer = """
            namespace Test {
                partial class DpiForm {
                    private void InitializeComponent() {
                        this.button = new System.Windows.Forms.Button();
                        this.button.Location = new System.Drawing.Point(10, 10);
                        this.button.Size = new System.Drawing.Size(100, 30);
                        this.button.Text = "Scale";
                        this.ClientSize = new System.Drawing.Size(240, 120);
                        this.Controls.Add(this.button);
                    }
                    private System.Windows.Forms.Button button;
                }
            }
            """;
        var renderer = new DesignSurfaceFormRenderer();

        var at96 = ReadSize(renderer.RenderDesignerCode(
            designer,
            dpi: 96,
            providerProfile: "StandardWinForms"));
        var at144 = ReadSize(renderer.RenderDesignerCode(
            designer,
            dpi: 144,
            providerProfile: "StandardWinForms"));

        Assert.Multiple(() => {
            Assert.That(at144.Width, Is.GreaterThan(at96.Width));
            Assert.That(at144.Height, Is.GreaterThan(at96.Height));
        });
    }

    [Test]
    public void RenderFormToolSchema_ExposesOptionalVisualProfile() {
        var tool = ToolDefinitionCatalog.All.Single(definition => definition.Name == ToolNames.RenderForm);
        var properties = tool.InputSchema.GetProperty("properties");

        Assert.Multiple(() => {
            Assert.That(properties.TryGetProperty("theme", out _), Is.True);
            Assert.That(properties.TryGetProperty("dpi", out _), Is.True);
            Assert.That(properties.TryGetProperty("providerProfile", out _), Is.True);
            Assert.That(ToolDefinitionCatalog.All, Has.Count.EqualTo(46));
        });
    }

    private static Size ReadSize(byte[] pngBytes) {
        using var stream = new MemoryStream(pngBytes);
        using var bitmap = new Bitmap(stream);
        return bitmap.Size;
    }
}