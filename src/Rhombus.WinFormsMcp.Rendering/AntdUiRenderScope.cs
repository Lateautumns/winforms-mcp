using System.Diagnostics;
using System.Reflection;

namespace Rhombus.WinFormsMcp.Rendering;

/// <summary>
/// Applies AntdUI's process-global theme/DPI settings for one render and
/// restores the exact prior values when the request completes. The renderer
/// intentionally uses reflection so the core rendering assembly does not take
/// a compile-time dependency on AntdUI.
/// </summary>
internal sealed class AntdUiRenderScope : IDisposable {
    private readonly PropertyInfo? _modeProperty;
    private readonly object? _previousMode;
    private readonly MethodInfo? _setDpiMethod;
    private readonly float? _previousCustomDpi;
    private bool _restoreMode;
    private bool _restoreDpi;
    private bool _disposed;

    private AntdUiRenderScope(
        PropertyInfo? modeProperty,
        object? previousMode,
        MethodInfo? setDpiMethod,
        float? previousCustomDpi,
        bool restoreMode,
        bool restoreDpi) {
        _modeProperty = modeProperty;
        _previousMode = previousMode;
        _setDpiMethod = setDpiMethod;
        _previousCustomDpi = previousCustomDpi;
        _restoreMode = restoreMode;
        _restoreDpi = restoreDpi;
    }

    public static AntdUiRenderScope Enter(
        RenderVisualOptions options,
        IEnumerable<Assembly> extraAssemblies) {
        var configType = FindConfigType(extraAssemblies);
        var requiresAntd = options.ProviderProfile == RenderVisualOptions.AntdUiProfile ||
            options.Theme != null;

        if (requiresAntd && configType == null)
            throw FormRenderErrors.Create(
                "render_provider_unavailable",
                "The AntdUI provider profile was requested, but AntdUI.Config " +
                "was not found in the renderer's loaded assemblies.");

        if (configType == null ||
            (options.ProviderProfile != RenderVisualOptions.AntdUiProfile && options.Theme == null))
            return new AntdUiRenderScope(null, null, null, null, false, false);

        var modeProperty = configType.GetProperty(
            "Mode",
            BindingFlags.Public | BindingFlags.Static);
        var setDpiMethod = configType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
                method.Name == "SetDpi" &&
                method.GetParameters().Length == 1 &&
                (method.GetParameters()[0].ParameterType == typeof(float?) ||
                 method.GetParameters()[0].ParameterType == typeof(float)));
        var customDpiField = configType.GetField(
            "_dpi_custom",
            BindingFlags.NonPublic | BindingFlags.Static);

        var previousMode = modeProperty?.CanRead == true ? modeProperty.GetValue(null) : null;
        var previousCustomDpi = customDpiField?.GetValue(null) is float value ? (float?)value : null;
        var scope = new AntdUiRenderScope(
            modeProperty,
            previousMode,
            setDpiMethod,
            previousCustomDpi,
            restoreMode: false,
            restoreDpi: false);

        try {
            if (options.Theme != null) {
                scope._restoreMode = true;
                scope.SetTheme(options.ResolveTheme()!);
            }

            var usesAntdUi = options.ProviderProfile == RenderVisualOptions.AntdUiProfile ||
                options.Theme != null;
            if (options.Dpi.HasValue && usesAntdUi) {
                scope._restoreDpi = true;
                scope.SetDpi(options.Dpi.Value / 96F);
            }

            return scope;
        }
        catch {
            scope.Dispose();
            throw;
        }
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;

        // Restore both values even if one setter fails. A renderer process is
        // long-lived, so leaking either global would contaminate later calls.
        Exception? restoreError = null;
        try {
            if (_restoreDpi)
                RestoreDpi();
        }
        catch (Exception exception) {
            restoreError = exception;
        }

        try {
            if (_restoreMode)
                RestoreMode();
        }
        catch (Exception exception) {
            restoreError ??= exception;
        }

        if (restoreError != null)
            Debug.WriteLine($"AntdUI render state restore failed: {restoreError}");
    }

    private void SetTheme(string theme) {
        if (_modeProperty?.CanWrite != true)
            throw FormRenderErrors.Create(
                "render_provider_unsupported",
                "AntdUI.Config.Mode is not writable in the loaded AntdUI version.");

        var mode = Enum.Parse(_modeProperty.PropertyType, theme, ignoreCase: true);
        _modeProperty.SetValue(null, mode);
    }

    private void SetDpi(float scale) {
        if (_setDpiMethod == null)
            throw FormRenderErrors.Create(
                "render_provider_unsupported",
                "AntdUI.Config.SetDpi(float?) is not available in the loaded AntdUI version.");

        var parameterType = _setDpiMethod.GetParameters()[0].ParameterType;
        object value = parameterType == typeof(float?) ? (float?)scale : scale;
        _setDpiMethod.Invoke(null, [value]);
    }

    private void RestoreDpi() {
        if (_setDpiMethod == null)
            return;

        var parameterType = _setDpiMethod.GetParameters()[0].ParameterType;
        object? value = parameterType == typeof(float?) ? _previousCustomDpi : _previousCustomDpi.GetValueOrDefault();
        _setDpiMethod.Invoke(null, [value]);
    }

    private void RestoreMode() {
        if (_modeProperty?.CanWrite == true && _previousMode != null)
            _modeProperty.SetValue(null, _previousMode);
    }

    private static Type? FindConfigType(IEnumerable<Assembly> extraAssemblies) {
        foreach (var assembly in extraAssemblies.Concat(AppDomain.CurrentDomain.GetAssemblies())) {
            try {
                var type = assembly.GetType("AntdUI.Config", throwOnError: false, ignoreCase: false);
                if (type != null)
                    return type;
            }
            catch (ReflectionTypeLoadException) {
                // A partially loadable third-party assembly is not a usable
                // provider; continue searching other loaded assemblies.
            }
        }

        return null;
    }
}