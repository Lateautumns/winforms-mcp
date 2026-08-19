using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Protocol;

using Rhombus.WinFormsMcp.Server.Tools;

namespace Rhombus.WinFormsMcp.Server;

internal sealed class ToolRegistry {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyDictionary<string, IToolHandler> _handlers;
    private readonly ITelemetry _telemetry;
    private readonly ILogger<ToolRegistry> _logger;
    private readonly TimeSpan _timeout;
    private readonly HashSet<string> _toolNames;

    public ToolRegistry(
        IEnumerable<IToolHandler> handlers,
        ITelemetry telemetry,
        IOptions<McpServerOptions> options,
        ILogger<ToolRegistry> logger) {
        _telemetry = telemetry;
        _logger = logger;
        _timeout = TimeSpan.FromMilliseconds(options.Value.ToolTimeoutMs);

        var handlerList = handlers.ToList();
        var duplicateHandlers = handlerList.GroupBy(handler => handler.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateHandlers.Length > 0)
            throw new InvalidOperationException($"Duplicate tool handlers: {string.Join(", ", duplicateHandlers)}");

        _handlers = handlerList.ToDictionary(handler => handler.Name, StringComparer.Ordinal);
        Tools = ToolDefinitionCatalog.All.ToList();
        _toolNames = Tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        var missingHandlers = _toolNames.Except(_handlers.Keys, StringComparer.Ordinal).ToArray();
        var missingDefinitions = _handlers.Keys.Except(_toolNames, StringComparer.Ordinal).ToArray();
        if (missingHandlers.Length > 0 || missingDefinitions.Length > 0) {
            throw new InvalidOperationException(
                $"Tool registry mismatch. Missing handlers: [{string.Join(", ", missingHandlers)}]; " +
                $"missing definitions: [{string.Join(", ", missingDefinitions)}].");
        }
    }

    public IList<Tool> Tools { get; }

    public async ValueTask<CallToolResult> ExecuteAsync(
        CallToolRequestParams request,
        CancellationToken cancellationToken) {
        var stopwatch = Stopwatch.StartNew();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        try {
            if (!_toolNames.Contains(request.Name)) {
                stopwatch.Stop();
                TrackCompletion(request.Name, stopwatch.Elapsed, failed: true);
                return CreateError("tool_not_found", $"Unknown tool: {request.Name}", retryable: false, stopwatch.Elapsed);
            }

            var arguments = JsonSerializer.SerializeToElement(
                request.Arguments ?? new Dictionary<string, JsonElement>(),
                SerializerOptions);
            var result = await _handlers[request.Name].ExecuteAsync(arguments, timeoutSource.Token);

            stopwatch.Stop();
            TrackCompletion(request.Name, stopwatch.Elapsed, failed: false);
            return CreateResult(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            stopwatch.Stop();
            TrackCompletion(request.Name, stopwatch.Elapsed, failed: true);
            return CreateError(
                "timeout",
                $"Tool '{request.Name}' exceeded the {_timeout.TotalMilliseconds:0}ms timeout.",
                retryable: true,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) {
            stopwatch.Stop();
            TrackCompletion(request.Name, stopwatch.Elapsed, failed: true);
            return CreateError("cancelled", $"Tool '{request.Name}' was cancelled.", retryable: true, stopwatch.Elapsed);
        }
        catch (Exception ex) {
            stopwatch.Stop();
            _logger.LogError(ex, "Tool {ToolName} failed after {ElapsedMs}ms", request.Name, stopwatch.ElapsedMilliseconds);
            _telemetry.TrackToolCall(request.Name, stopwatch.Elapsed);
            _telemetry.TrackException(ex);
            var toolException = ex as ToolExecutionException;
            return CreateError(
                toolException?.Code ?? GetErrorCode(ex),
                ex.Message,
                toolException?.Retryable ?? IsRetryable(ex),
                stopwatch.Elapsed,
                ex.GetType().Name);
        }
    }

    private CallToolResult CreateResult(JsonElement result) {
        if (result.TryGetProperty("imageBase64", out var image) && image.ValueKind == JsonValueKind.String) {
            return new CallToolResult {
                Content = [ImageContentBlock.FromBytes(Convert.FromBase64String(image.GetString()!), "image/png")],
                StructuredContent = RemoveImageData(result)
            };
        }

        return new CallToolResult {
            Content = [new TextContentBlock { Text = result.GetRawText() }],
            StructuredContent = result.Clone()
        };
    }

    private static CallToolResult CreateError(
        string code,
        string message,
        bool retryable,
        TimeSpan elapsed,
        string? exceptionType = null) {
        var payload = JsonSerializer.SerializeToElement(new {
            success = false,
            error = new {
                code,
                message,
                exceptionType,
                retryable,
                elapsedMs = (long)elapsed.TotalMilliseconds
            }
        }, SerializerOptions);

        return new CallToolResult {
            IsError = true,
            Content = [new TextContentBlock { Text = payload.GetRawText() }],
            StructuredContent = payload
        };
    }

    private static JsonElement RemoveImageData(JsonElement result) {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in result.EnumerateObject()) {
            if (!string.Equals(property.Name, "imageBase64", StringComparison.Ordinal))
                values[property.Name] = property.Value.Clone();
        }
        return JsonSerializer.SerializeToElement(values, SerializerOptions);
    }

    private void TrackCompletion(string toolName, TimeSpan elapsed, bool failed) {
        if (failed)
            _logger.LogWarning("Tool {ToolName} failed after {ElapsedMs}ms", toolName, elapsed.TotalMilliseconds);
        else
            _logger.LogInformation("Tool {ToolName} completed in {ElapsedMs}ms", toolName, elapsed.TotalMilliseconds);
        _telemetry.TrackToolCall(toolName, elapsed);
    }

    private static string GetErrorCode(Exception exception) => exception switch {
        ArgumentException => "invalid_argument",
        FileNotFoundException or DirectoryNotFoundException => "not_found",
        TimeoutException => "timeout",
        InvalidOperationException => "invalid_operation",
        UnauthorizedAccessException => "access_denied",
        _ => "internal_error"
    };

    private static bool IsRetryable(Exception exception) => exception is IOException or TimeoutException;
}