using System.Text.Json;

using Rhombus.WinFormsMcp.Rendering;
using Rhombus.WinFormsMcp.Server.Automation;

namespace Rhombus.WinFormsMcp.Server.Tools.Rendering;

internal sealed class RenderFormToolHandler : IToolHandler {
    private const string AuthoringHint =
        "Use the Visual Studio designer convention: a separate .Designer.cs partial class with " +
        "InitializeComponent(), fully qualified control types, layout calls, field declarations, " +
        "and the components-container Dispose pattern.";

    private readonly RendererProcessPool _rendererPool;

    public RenderFormToolHandler(RendererProcessPool rendererPool) {
        _rendererPool = rendererPool;
    }

    public string Name => ToolNames.RenderForm;

    public async ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) {
        var requestedPath = ToolArguments.RequireString(arguments, "designerFilePath");
        var outputPath = ToolArguments.GetString(arguments, "outputPath");
        var designerFile = FormRenderingHelpers.ResolveDesignerFile(requestedPath);
        var designerContent = await File.ReadAllTextAsync(designerFile, cancellationToken);

        var suffixIndex = designerFile.LastIndexOf(".Designer.cs", StringComparison.OrdinalIgnoreCase);
        var companionPath = suffixIndex >= 0 ? designerFile[..suffixIndex] + ".cs" : designerFile;
        var companionContent = File.Exists(companionPath)
            ? await File.ReadAllTextAsync(companionPath, cancellationToken)
            : null;

        var configuredTfm = _rendererPool.GetConfiguredTfm();
        string? projectPath = null;
        try {
            projectPath = FormRenderingHelpers.FindCsproj(Path.GetDirectoryName(designerFile)!);
        }
        catch (FileNotFoundException) when (
            !string.Equals(configuredTfm, "auto", StringComparison.OrdinalIgnoreCase)) {
            // An explicitly configured TFM can render standalone designer files.
        }

        string? projectTfm = null;
        if (projectPath != null) {
            try {
                projectTfm = RendererProcessPool.DetectTfmFromCsproj(projectPath);
            }
            catch (InvalidOperationException) when (
                !string.Equals(configuredTfm, "auto", StringComparison.OrdinalIgnoreCase)) {
                // An explicit TFM does not require a well-formed project file.
            }
        }
        var extraAssemblyPaths = projectPath == null
            ? []
            : FormRenderingHelpers.ResolveProjectAssemblyPaths(projectPath, projectTfm);

        byte[] pngBytes;
        try {
            pngBytes = await _rendererPool.RenderAsync(
                designerContent,
                companionContent,
                extraAssemblyPaths,
                configuredTfm,
                projectPath,
                cancellationToken);
        }
        catch (RendererProcessPool.RendererHostException exception) {
            throw new ToolExecutionException(
                exception.Code,
                exception.Message,
                retryable: false,
                exception);
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
            await File.WriteAllBytesAsync(outputPath, pngBytes, cancellationToken);

        return ToolJson.Result(new {
            success = true,
            imageBase64 = Convert.ToBase64String(pngBytes),
            hint = AuthoringHint
        });
    }
}