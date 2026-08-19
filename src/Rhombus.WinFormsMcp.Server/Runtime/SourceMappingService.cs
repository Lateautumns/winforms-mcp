using System.Diagnostics;
using System.Text.RegularExpressions;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Server.Runtime;

/// <summary>
/// Maps a managed control name to Designer and code-behind source locations.
/// Source files are read through a bounded incremental <see cref="SourceIndex"/>.
/// </summary>
internal sealed class SourceMappingService {
    private static readonly Regex TypeNamePattern = new(
        @"(?:global::)?(?<type>[A-Za-z_][A-Za-z0-9_.]*)",
        RegexOptions.Compiled);

    private readonly SourceIndex _sourceIndex;

    public SourceMappingService() : this(new SourceIndex()) { }

    public SourceMappingService(SourceIndex sourceIndex) {
        _sourceIndex = sourceIndex;
    }

    public Task<SourceMappingSnapshot> MapAsync(
        int processId,
        ControlIdentity control,
        string? sourceRoot,
        CancellationToken cancellationToken,
        int? maxFiles = null) =>
        Task.Run(
            () => MapAsyncCore(processId, control, sourceRoot, cancellationToken, maxFiles),
            cancellationToken);

    private async Task<SourceMappingSnapshot> MapAsyncCore(
        int processId,
        ControlIdentity control,
        string? sourceRoot,
        CancellationToken cancellationToken,
        int? maxFiles) {
        var result = new SourceMappingSnapshot { Control = control };
        var ownerType = NormalizeTypeName(control.OwnerType);
        var ownerTypeName = GetSimpleTypeName(ownerType);
        if (string.IsNullOrWhiteSpace(control.Name)) {
            result.Warnings.Add("The managed control has no Name; source mapping cannot be deterministic.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(ownerTypeName)) {
            result.Warnings.Add("The managed control has no owning Form type; source mapping cannot identify a Designer partial class.");
            return result;
        }

        var root = ResolveRoot(processId, sourceRoot);
        if (root is null) {
            result.Warnings.Add("Source root could not be inferred. Pass 'sourceRoot' to scan a solution or project directory.");
            return result;
        }

        var index = await _sourceIndex.RefreshAsync(root, maxFiles, cancellationToken).ConfigureAwait(false);
        result.Index = index.Metadata;
        result.Warnings.AddRange(index.Metadata.Warnings);
        if (index.Metadata.Truncated)
            result.Warnings.Add($"Source scan was truncated at {index.Metadata.MaxFiles} files.");

        var candidates = index.Types
            .Where(type => string.Equals(type.Name, ownerTypeName, StringComparison.Ordinal) &&
                          (!ownerType.Contains('.', StringComparison.Ordinal) ||
                           string.Equals(type.FullyQualifiedName, ownerType, StringComparison.Ordinal)))
            .OrderBy(type => type.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Length == 0) {
            result.Warnings.Add($"No source declaration for owning Form '{ownerTypeName}' was found under '{root}'.");
            return result;
        }

        var designer = candidates.FirstOrDefault(candidate =>
            candidate.Path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));
        var codeBehind = candidates.FirstOrDefault(candidate =>
            !candidate.Path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));
        var owner = candidates.First();

        result.Namespace = owner.Namespace;
        result.Type = owner.Name;
        result.FullyQualifiedType = owner.FullyQualifiedName;

        if (designer is null) {
            result.Warnings.Add("A .Designer.cs partial class was not found.");
        }
        else {
            result.Designer = ToClassLocation(designer);
            if (designer.Fields.TryGetValue(control.Name, out var declaration))
                result.Declaration = declaration;
            if (designer.Initialization.TryGetValue(control.Name, out var initialization))
                result.Initialization = initialization;
        }

        if (codeBehind is null) {
            result.Warnings.Add("A non-Designer code-behind file was not found.");
        }
        else {
            result.CodeBehindFile = codeBehind.Path;
        }

        foreach (var eventRegistration in candidates
                     .SelectMany(candidate => candidate.Events)
                     .Where(item => string.Equals(item.ControlName, control.Name, StringComparison.Ordinal))
                     .OrderBy(item => item.Location.File, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Location.Line)) {
            if (result.Events.ContainsKey(eventRegistration.Event))
                continue;

            var handlerLocation = candidates
                .Where(candidate => !candidate.Path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                .Select(candidate => candidate.Methods.TryGetValue(eventRegistration.Method, out var location)
                    ? location
                    : null)
                .FirstOrDefault(location => location is not null);
            var location = handlerLocation ?? eventRegistration.Location;
            var fullyQualifiedSymbol = string.IsNullOrWhiteSpace(result.FullyQualifiedType)
                ? eventRegistration.Method
                : $"{result.FullyQualifiedType}.{eventRegistration.Method}";
            result.Events[eventRegistration.Event] = new EventHandlerSnapshot {
                Event = eventRegistration.Event,
                Method = eventRegistration.Method,
                File = location.File,
                Line = location.Line,
                FullyQualifiedSymbol = fullyQualifiedSymbol
            };
            if (handlerLocation is null)
                result.Warnings.Add($"Event handler '{eventRegistration.Method}' was not found in a non-Designer partial class.");
        }

        return result;
    }

    private static string? ResolveRoot(int processId, string? sourceRoot) {
        if (!string.IsNullOrWhiteSpace(sourceRoot)) {
            var full = Path.GetFullPath(sourceRoot);
            if (File.Exists(full))
                return Path.GetDirectoryName(full);
            if (Directory.Exists(full))
                return full;
        }

        try {
            using var process = Process.GetProcessById(processId);
            var path = process.MainModule?.FileName;
            var directory = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
            for (var current = directory; current is not null; current = Directory.GetParent(current)?.FullName) {
                if (Directory.EnumerateFiles(current, "*.csproj", SearchOption.TopDirectoryOnly).Any() ||
                    Directory.EnumerateFiles(current, "*.sln", SearchOption.TopDirectoryOnly).Any())
                    return current;
            }
            return directory;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
                                   UnauthorizedAccessException or IOException or
                                   System.ComponentModel.Win32Exception) {
            return null;
        }
    }

    private static string GetSimpleTypeName(string typeName) {
        var match = TypeNamePattern.Match(typeName);
        var value = match.Success ? match.Groups["type"].Value : typeName;
        var lastDot = value.LastIndexOf('.');
        return lastDot < 0 ? value : value[(lastDot + 1)..];
    }

    private static string NormalizeTypeName(string? typeName) {
        var value = typeName?.Trim() ?? string.Empty;
        return value.StartsWith("global::", StringComparison.Ordinal)
            ? value["global::".Length..]
            : value;
    }

    private static SourceLocationSnapshot ToClassLocation(IndexedSourceType candidate) => candidate.TypeLocation;
}