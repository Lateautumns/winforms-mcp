using System.Reflection;

namespace Rhombus.WinFormsMcp.Server;

internal static class ServerVersion {
    public static string Current { get; } =
        typeof(ServerVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0]
        ?? typeof(ServerVersion).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";
}