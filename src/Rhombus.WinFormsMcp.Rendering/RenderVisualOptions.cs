using System.Drawing;
using System.Windows.Forms;

namespace Rhombus.WinFormsMcp.Rendering;

/// <summary>
/// Optional visual profile for a render request. Values are deliberately kept
/// transport-friendly because the request crosses the RendererHost process
/// boundary as JSON.
/// </summary>
public sealed class RenderVisualOptions {
    public const string AutoTheme = "Auto";
    public const string LightTheme = "Light";
    public const string DarkTheme = "Dark";
    public const string AntdUiProfile = "AntdUI";
    public const string StandardWinFormsProfile = "StandardWinForms";

    private static readonly int[] SupportedDpi = [96, 120, 144, 192];

    private RenderVisualOptions(string? theme, int? dpi, string? providerProfile) {
        Theme = theme;
        Dpi = dpi;
        ProviderProfile = providerProfile;
    }

    public string? Theme { get; }

    public int? Dpi { get; }

    public string? ProviderProfile { get; }

    public float ScaleFactor => Dpi.GetValueOrDefault(96) / 96F;

    public bool HasOverrides => Theme != null || Dpi.HasValue || ProviderProfile != null;

    /// <summary>
    /// Normalizes and validates values supplied by either MCP or the renderer
    /// host. The accepted DPI values match the supported validation matrix.
    /// </summary>
    public static RenderVisualOptions Normalize(
        string? theme,
        int? dpi,
        string? providerProfile) {
        var normalizedTheme = NormalizeTheme(theme);
        var normalizedDpi = NormalizeDpi(dpi);
        var normalizedProfile = NormalizeProfile(providerProfile);

        if (normalizedProfile == StandardWinFormsProfile && normalizedTheme != null)
            throw FormRenderErrors.Create(
                "render_profile_unsupported",
                "StandardWinForms does not provide a global theme override. " +
                "Use providerProfile='AntdUI' for theme=Light, Dark, or Auto.");

        return new RenderVisualOptions(normalizedTheme, normalizedDpi, normalizedProfile);
    }

    /// <summary>
    /// Resolves Auto to a deterministic light/dark mode for providers that
    /// expose only a two-state global theme (such as AntdUI).
    /// </summary>
    internal string? ResolveTheme() {
        if (Theme == null || !string.Equals(Theme, AutoTheme, StringComparison.Ordinal))
            return Theme;

        // SystemColors.Window is available on every supported TFM and avoids
        // depending on a Windows-version-specific registry contract.
        return SystemInformation.HighContrast || SystemColors.Window.GetBrightness() >= 0.5F
            ? LightTheme
            : DarkTheme;
    }

    internal string CacheToken => string.Join(
        "|",
        Theme == AutoTheme ? $"{AutoTheme}:{ResolveTheme()}" : Theme ?? "<default-theme>",
        Dpi?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<default-dpi>",
        ProviderProfile ?? "<default-provider>");

    private static string? NormalizeTheme(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value!.Trim().ToLowerInvariant() switch {
            "auto" => AutoTheme,
            "light" => LightTheme,
            "dark" => DarkTheme,
            _ => throw FormRenderErrors.Create(
                "render_invalid_theme",
                $"Unsupported render theme '{value}'. Expected Light, Dark, or Auto.")
        };
    }

    private static int? NormalizeDpi(int? value) {
        if (!value.HasValue)
            return null;
        if (SupportedDpi.Contains(value.Value))
            return value;

        throw FormRenderErrors.Create(
            "render_invalid_dpi",
            $"Unsupported render DPI '{value.Value}'. Expected one of {string.Join(", ", SupportedDpi)}.");
    }

    private static string? NormalizeProfile(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value!.Trim().ToLowerInvariant() switch {
            "auto" => null,
            "antdui" or "antd-ui" => AntdUiProfile,
            "standardwinforms" or "standard-winforms" or "winforms" => StandardWinFormsProfile,
            _ => throw FormRenderErrors.Create(
                "render_invalid_provider_profile",
                $"Unsupported render provider profile '{value}'. Expected AntdUI or StandardWinForms.")
        };
    }
}