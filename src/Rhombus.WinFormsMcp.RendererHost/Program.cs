using System;
using System.IO;
using System.Text.Json;

using Rhombus.WinFormsMcp.Rendering;

namespace Rhombus.WinFormsMcp.RendererHost;

/// <summary>
/// Out-of-process renderer host. Reads JSON render requests from stdin,
/// renders via DesignSurface, writes JSON responses to stdout.
/// Loops until stdin is closed. One instance per TFM, reused across projects.
/// </summary>
class Program {
    [STAThread]
    static int Main(string[] _) {
        try {
            // Signal ready
            var ready = JsonSerializer.Serialize(new { type = "ready", tfm = GetCurrentTfm() });
            Console.Out.WriteLine(ready);
            Console.Out.Flush();

            var renderer = new DesignSurfaceFormRenderer();

            string? line;
            while ((line = Console.In.ReadLine()) != null) {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string response;
                try {
                    var request = JsonSerializer.Deserialize<RenderRequest>(line, JsonOptions.Default);
                    if (request == null) {
                        response = JsonSerializer.Serialize(new RenderResponse {
                            Success = false,
                            Error = "Failed to parse request"
                        }, JsonOptions.Default);
                    }
                    else {
                        var pngBytes = renderer.RenderDesignerCode(
                            request.DesignerContent,
                            request.CompanionContent,
                            request.ExtraAssemblyPaths,
                            theme: request.Theme,
                            dpi: request.Dpi,
                            providerProfile: request.ProviderProfile);
                        response = JsonSerializer.Serialize(new RenderResponse {
                            Success = true,
                            PngBase64 = Convert.ToBase64String(pngBytes),
                            Theme = request.Theme,
                            Dpi = request.Dpi,
                            ProviderProfile = request.ProviderProfile
                        }, JsonOptions.Default);
                    }
                }
                catch (Exception ex) {
                    Console.Error.WriteLine(ex);
                    response = JsonSerializer.Serialize(new RenderResponse {
                        Success = false,
                        ErrorCode = FormRenderErrors.GetCode(ex) ?? "renderer_internal_error",
                        Error = ex.Message,
                        ExceptionType = ex.GetType().FullName,
                        Details = ex.ToString()
                    }, JsonOptions.Default);
                }

                Console.Out.WriteLine(response);
                Console.Out.Flush();
            }

            return 0;
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Fatal: {ex.Message}");
            return 1;
        }
    }

    private static string GetCurrentTfm() {
#if NET9_0_OR_GREATER
        return "net9.0-windows";
#elif NET8_0_OR_GREATER
        return "net8.0-windows";
#elif NET7_0_OR_GREATER
        return "net7.0-windows";
#elif NET6_0_OR_GREATER
        return "net6.0-windows";
#elif NET5_0_OR_GREATER
        return "net5.0-windows";
#elif NETCOREAPP3_1
        return "netcoreapp3.1";
#elif NET48
        return "net48";
#elif NET472
        return "net472";
#elif NET471
        return "net471";
#elif NET47
        return "net47";
#elif NET462
        return "net462";
#else
        return "unknown";
#endif
    }
}

class RenderRequest {
    public string DesignerContent { get; set; } = "";
    public string? CompanionContent { get; set; }
    public string[]? ExtraAssemblyPaths { get; set; }
    public string? Theme { get; set; }
    public int? Dpi { get; set; }
    public string? ProviderProfile { get; set; }
}

class RenderResponse {
    public bool Success { get; set; }
    public string? PngBase64 { get; set; }
    public string? ErrorCode { get; set; }
    public string? Error { get; set; }
    public string? ExceptionType { get; set; }
    public string? Details { get; set; }
    public string? Theme { get; set; }
    public int? Dpi { get; set; }
    public string? ProviderProfile { get; set; }
}

static class JsonOptions {
    public static readonly JsonSerializerOptions Default = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}