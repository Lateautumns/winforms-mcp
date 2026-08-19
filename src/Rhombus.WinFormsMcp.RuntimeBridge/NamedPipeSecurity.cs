#if NETFRAMEWORK
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Rhombus.WinFormsMcp.RuntimeBridge;

internal static class NamedPipeSecurity {
    public static PipeSecurity CreateCurrentUserOnly() {
        var user = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current Windows user SID could not be resolved.");
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            user,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        return security;
    }
}
#endif