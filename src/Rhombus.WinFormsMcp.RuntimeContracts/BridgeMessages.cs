using System.Text.Json;

namespace Rhombus.WinFormsMcp.RuntimeContracts;

public sealed class RuntimeRequest {
    public int ProtocolVersion { get; set; } = RuntimeBridgeProtocol.Version;
    public string RequestId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int Pid { get; set; }
    public JsonElement Arguments { get; set; }
}

public sealed class RuntimeResponse {
    public int ProtocolVersion { get; set; } = RuntimeBridgeProtocol.Version;
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public JsonElement Result { get; set; }
    public RuntimeError? Error { get; set; }
}

public sealed class RuntimeError {
    public string Code { get; set; } = "runtime_error";
    public string Message { get; set; } = string.Empty;
    public bool Retryable { get; set; }
    public string? ExceptionType { get; set; }
}

public sealed class BridgeHello {
    public int ProtocolVersion { get; set; } = RuntimeBridgeProtocol.Version;
    public RuntimeProcessInfo Process { get; set; } = new();
    public string[] Capabilities { get; set; } = Array.Empty<string>();
}

public sealed class RuntimeProcessInfo {
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Runtime { get; set; } = string.Empty;
    public string Framework { get; set; } = "WinForms";
    public string? BridgeVersion { get; set; }
}

public sealed class BridgeStatus {
    public bool Available { get; set; }
    public bool Connected { get; set; }
    public int ProtocolVersion { get; set; }
    public RuntimeProcessInfo? Process { get; set; }
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public string? PipeName { get; set; }
    public string? Error { get; set; }
}