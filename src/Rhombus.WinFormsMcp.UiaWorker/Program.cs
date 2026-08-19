using System.Diagnostics;
using System.Text;
using System.Text.Json;

using FlaUI.Core.Conditions;
using FlaUI.UIA2;

namespace Rhombus.WinFormsMcp.UiaWorker;

internal static class Program {
    private const int ProtocolVersion = 1;
    private const int MaximumRequestCharacters = 1_048_576;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };

    private static async Task<int> Main() {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);

        using var automation = new UIA2Automation();
        while (await Console.In.ReadLineAsync().ConfigureAwait(false) is { } line) {
            UiaWorkerResponse response;
            try {
                if (line.Length > MaximumRequestCharacters)
                    throw new InvalidOperationException("UIA worker request exceeds the configured size limit.");
                var request = JsonSerializer.Deserialize<UiaWorkerRequest>(line, JsonOptions)
                    ?? throw new InvalidOperationException("UIA worker request was empty.");
                response = await ExecuteAsync(automation, request).ConfigureAwait(false);
            }
            catch (Exception exception) {
                response = UiaWorkerResponse.CreateFailure(string.Empty, "invalid_request", exception.Message, exception);
            }

            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
            await Console.Out.FlushAsync().ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<UiaWorkerResponse> ExecuteAsync(
        UIA2Automation automation,
        UiaWorkerRequest request) {
        if (request.ProtocolVersion != ProtocolVersion) {
            return UiaWorkerResponse.CreateFailure(
                request.RequestId,
                "protocol_version_unsupported",
                $"Unsupported UIA worker protocol version {request.ProtocolVersion}.");
        }

        try {
            object result = request.Command switch {
                "ping" => new { processId = Environment.ProcessId, protocolVersion = ProtocolVersion },
                "element_exists" => ProbeElement(
                    automation,
                    RequireString(request.Arguments, "automationId"),
                    GetInt(request.Arguments, "timeoutMs", 1000)),
                "test_delay" => await DelayAsync(GetInt(request.Arguments, "delayMs", 0)).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown UIA worker command '{request.Command}'.")
            };
            return UiaWorkerResponse.CreateSuccess(request.RequestId, result);
        }
        catch (Exception exception) {
            return UiaWorkerResponse.CreateFailure(
                request.RequestId,
                exception is ArgumentException ? "invalid_argument" : "uia_worker_error",
                exception.Message,
                exception);
        }
    }

    private static object ProbeElement(UIA2Automation automation, string automationId, int timeoutMs) {
        var boundedTimeout = Math.Clamp(timeoutMs, 1, 60_000);
        var root = automation.GetDesktop();
        var condition = new PropertyCondition(
            automation.PropertyLibrary.Element.AutomationId,
            automationId);
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < boundedTimeout) {
            try {
                if (root.FindFirstChild(condition) is not null)
                    return new { exists = true };
            }
            catch {
                // UIA providers can invalidate elements while the worker polls.
            }

            Thread.Sleep(Math.Min(100, Math.Max(1, boundedTimeout - (int)stopwatch.ElapsedMilliseconds)));
        }

        return new { exists = false };
    }

    private static async Task<object> DelayAsync(int delayMs) {
        await Task.Delay(Math.Clamp(delayMs, 0, 60_000)).ConfigureAwait(false);
        return new { completed = true };
    }

    private static string RequireString(JsonElement arguments, string name) {
        if (arguments.ValueKind == JsonValueKind.Object &&
            arguments.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()))
            return value.GetString()!;
        throw new ArgumentException($"'{name}' is required.");
    }

    private static int GetInt(JsonElement arguments, string name, int defaultValue) =>
        arguments.ValueKind == JsonValueKind.Object &&
        arguments.TryGetProperty(name, out var value) &&
        value.TryGetInt32(out var result)
            ? result
            : defaultValue;

    private sealed class UiaWorkerRequest {
        public int ProtocolVersion { get; set; } = Program.ProtocolVersion;
        public string RequestId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public JsonElement Arguments { get; set; }
    }

    private sealed class UiaWorkerResponse {
        public int ProtocolVersion { get; set; } = Program.ProtocolVersion;
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public JsonElement Result { get; set; }
        public UiaWorkerError? Error { get; set; }

        public static UiaWorkerResponse CreateSuccess(string requestId, object result) => new() {
            RequestId = requestId,
            Success = true,
            Result = JsonSerializer.SerializeToElement(result, result.GetType(), JsonOptions)
        };

        public static UiaWorkerResponse CreateFailure(
            string requestId,
            string code,
            string message,
            Exception? exception = null) => new() {
                RequestId = requestId,
                Success = false,
                Result = JsonSerializer.SerializeToElement<object?>(null, JsonOptions),
                Error = new UiaWorkerError {
                    Code = code,
                    Message = message,
                    ExceptionType = exception?.GetType().Name
                }
            };
    }

    private sealed class UiaWorkerError {
        public string Code { get; set; } = "uia_worker_error";
        public string Message { get; set; } = string.Empty;
        public string? ExceptionType { get; set; }
    }
}