using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Automation.UiaWorker;

internal static class UiaWorkerProtocol {
    public const int Version = 1;
    public const string Ping = "ping";
    public const string ElementExists = "element_exists";
    public const string TestDelay = "test_delay";
}

internal sealed class UiaWorkerRequest {
    public int ProtocolVersion { get; set; } = UiaWorkerProtocol.Version;
    public string RequestId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public JsonElement Arguments { get; set; }
}

internal sealed class UiaWorkerResponse {
    public int ProtocolVersion { get; set; } = UiaWorkerProtocol.Version;
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public JsonElement Result { get; set; }
    public UiaWorkerError? Error { get; set; }
}

internal sealed class UiaWorkerError {
    public string Code { get; set; } = "uia_worker_error";
    public string Message { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
}

internal sealed class UiaWorkerProbeResult {
    public bool Exists { get; set; }
}

internal sealed class UiaWorkerPingResult {
    public int ProcessId { get; set; }
    public int ProtocolVersion { get; set; }
}

internal sealed class UiaWorkerException : InvalidOperationException {
    public UiaWorkerException(string code, string message, string? exceptionType = null)
        : base(message) {
        Code = code;
        ExceptionType = exceptionType;
    }

    public string Code { get; }
    public string? ExceptionType { get; }
}