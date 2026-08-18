using System.Windows.Forms;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

internal sealed class ControlProviderRegistry : IControlProviderRegistry {
    private readonly IReadOnlyList<IControlProvider> _providers;

    public ControlProviderRegistry(IEnumerable<IControlProvider> providers) {
        _providers = providers
            .OrderByDescending(provider => provider.Priority)
            .ThenBy(provider => provider.ProviderName, StringComparer.Ordinal)
            .ToArray();

        if (_providers.Count == 0)
            throw new ArgumentException("At least one control provider is required.", nameof(providers));
    }

    public static ControlProviderRegistry CreateDefault() =>
        new([new StandardWinFormsProvider()]);

    public IControlProvider Resolve(Control control) {
        foreach (var provider in _providers) {
            if (provider.CanHandle(control))
                return provider;
        }

        throw new InvalidOperationException($"No control provider can inspect {control.GetType().FullName}.");
    }
}