using System.Windows.Forms;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

internal interface IControlProvider {
    string ProviderName { get; }

    int Priority { get; }

    bool CanHandle(Control control);

    ControlProviderSnapshot Describe(Control control);

    ControlSemanticSnapshot Inspect(Control control, ControlProviderContext context);
}