using System.Diagnostics;
using System.Reflection;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.RuntimeBridge;

public sealed class RuntimeBridgeOptions {
    public int ProcessId { get; set; } = Process.GetCurrentProcess().Id;
    public string? PipeName { get; set; }
    public bool Debug { get; set; }
    public int MaxRequestBytes { get; set; } = 1_048_576;
    public int MaxDepth { get; set; } = 12;
    public int MaxNodes { get; set; } = 10_000;
    public string BridgeVersion { get; set; } = GetAssemblyVersion();

    internal string EffectivePipeName => string.IsNullOrWhiteSpace(PipeName)
        ? RuntimeBridgeProtocol.GetPipeName(ProcessId)
        : PipeName!;

    private static string GetAssemblyVersion() =>
        typeof(RuntimeBridgeOptions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0]
        ?? typeof(RuntimeBridgeOptions).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";
}
