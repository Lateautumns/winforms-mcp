using System.Text.RegularExpressions;

namespace Rhombus.WinFormsMcp.Rendering;

/// <summary>
/// Shared helper methods for form rendering: file resolution, project discovery, and designer file parsing.
/// </summary>
public static class FormRenderingHelpers {
    /// <summary>
    /// Resolve the .Designer.cs file from a source file path.
    /// If given a .cs file, looks for the sibling .Designer.cs.
    /// Throws if no separate designer file exists.
    /// </summary>
    public static string ResolveDesignerFile(string sourceFilePath) {
        if (sourceFilePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)) {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException($"Designer file not found: {sourceFilePath}");
            return sourceFilePath;
        }

        var dir = Path.GetDirectoryName(sourceFilePath)
            ?? throw new ArgumentException($"Cannot determine directory for: {sourceFilePath}");
        var baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var designerFile = Path.Combine(dir, $"{baseName}.Designer.cs");

        if (!File.Exists(designerFile))
            throw new FileNotFoundException(
                $"No separate Designer.cs file found for {sourceFilePath}. " +
                $"This tool requires a standard WinForms designer file. Expected: {designerFile}");

        return designerFile;
    }

    /// <summary>
    /// Walk up from a directory to find the nearest .csproj file.
    /// </summary>
    public static string FindCsproj(string directory) {
        var dir = directory;
        while (dir != null) {
            try {
                var csprojFiles = Directory.GetFiles(dir, "*.csproj");
                if (csprojFiles.Length > 0)
                    return csprojFiles[0];
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or DirectoryNotFoundException) {
                // Continue toward the filesystem root; inaccessible parents are not projects.
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException($"No .csproj file found in directory tree above {directory}");
    }

    /// <summary>
    /// Find reference assemblies in the most relevant project build output.
    /// </summary>
    public static string[] ResolveProjectAssemblyPaths(
        string csprojPath,
        string? preferredTfm = null) {
        var projectDirectory = Path.GetDirectoryName(csprojPath)
            ?? throw new ArgumentException($"Cannot determine project directory for: {csprojPath}");
        var binDirectory = Path.Combine(projectDirectory, "bin");
        if (!Directory.Exists(binDirectory))
            return [];

        var configurationDirectories = new[] { "Debug", "Release" }
            .Select(configuration => Path.Combine(binDirectory, configuration))
            .Where(Directory.Exists)
            .ToArray();

        var candidates = string.IsNullOrWhiteSpace(preferredTfm)
            ? []
            : configurationDirectories
                .Select(directory => Path.Combine(directory, preferredTfm))
                .Where(Directory.Exists)
                .Where(HasAssemblyFiles)
                .ToArray();

        if (candidates.Length == 0) {
            // Old-style .NET Framework projects commonly emit DLLs directly
            // into bin\\Debug or bin\\Release instead of a TFM subdirectory.
            // Keep those directories as candidates before probing nested output
            // folders used by SDK-style projects.
            candidates = configurationDirectories
                .Where(HasAssemblyFiles)
                .Concat(configurationDirectories.SelectMany(Directory.GetDirectories)
                    .Where(HasAssemblyFiles))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var latestOutput = candidates
            .OrderByDescending(GetAssemblyFileCount)
            .ThenByDescending(GetLatestAssemblyWriteTimeUtc)
            .FirstOrDefault();
        return latestOutput == null ? [] : EnumerateAssemblyFiles(latestOutput).ToArray();
    }

    private static DateTime GetLatestAssemblyWriteTimeUtc(string directory) =>
        EnumerateAssemblyFiles(directory)
            .Select(File.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

    private static int GetAssemblyFileCount(string directory) => EnumerateAssemblyFiles(directory).Count();

    private static bool HasAssemblyFiles(string directory) => EnumerateAssemblyFiles(directory).Any();

    private static IEnumerable<string> EnumerateAssemblyFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.dll")
            .Concat(Directory.EnumerateFiles(directory, "*.exe"));

    /// <summary>
    /// Parse the designer file to extract namespace, class name, and event handler names.
    /// </summary>
    public static (string? Namespace, string ClassName, List<string> EventHandlers) ParseDesignerFile(string content) {
        var nsMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
        var ns = nsMatch.Success ? nsMatch.Groups[1].Value : null;

        var classMatch = Regex.Match(content, @"partial\s+class\s+(\w+)");
        if (!classMatch.Success)
            throw new InvalidOperationException("Could not find partial class declaration in designer file.");
        var className = classMatch.Groups[1].Value;

        var eventHandlers = new List<string>();
        var eventMatches = Regex.Matches(content, @"\+=\s*(?:new\s+[\w.]+\s*\(\s*)?(?:this\.)?(\w+)\s*\)?\s*;");
        foreach (Match? m in eventMatches) {
            var handlerName = m!.Groups[1].Value;
            if (!eventHandlers.Contains(handlerName))
                eventHandlers.Add(handlerName);
        }

        return (ns, className, eventHandlers);
    }
}