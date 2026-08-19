using System.Windows.Forms;

namespace Rhombus.WinFormsMcp.RuntimeBridge.Inspection.Providers;

internal interface IControlProviderRegistry {
    IControlProvider Resolve(Control control);
}