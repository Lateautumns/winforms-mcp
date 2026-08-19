using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Server.Runtime;

/// <summary>
/// Bounded, read-only source index used by source mapping requests.
/// Each source root has an independent refresh gate, and a refresh is committed
/// only after all enumerated files have been fingerprinted and parsed.
/// </summary>
internal sealed class SourceIndex {
    internal const int DefaultMaxFiles = 5_000;
    internal const int MaximumMaxFiles = 5_000;

    private const int MaximumCachedRoots = 32;

    private readonly object _rootsLock = new();
    private readonly Dictionary<string, RootIndex> _roots = new(StringComparer.OrdinalIgnoreCase);

    public async Task<SourceIndexResult> RefreshAsync(
        string root,
        int? requestedMaxFiles,
        CancellationToken cancellationToken) {
        var canonicalRoot = CanonicalizeRoot(root);
        var maxFiles = NormalizeMaxFiles(requestedMaxFiles);
        var state = GetRootIndex(canonicalRoot);

        try {
            await state.RefreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                return await Task.Run(
                    () => RefreshRoot(state, canonicalRoot, maxFiles, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            finally {
                state.RefreshGate.Release();
                state.LastAccessUtc = DateTime.UtcNow;
            }
        }
        finally {
            Interlocked.Decrement(ref state.ActiveOperations);
        }
    }

    private RootIndex GetRootIndex(string canonicalRoot) {
        lock (_rootsLock) {
            if (!_roots.TryGetValue(canonicalRoot, out var state)) {
                if (_roots.Count >= MaximumCachedRoots) {
                    var evictable = _roots
                        .Where(pair => Volatile.Read(ref pair.Value.ActiveOperations) == 0)
                        .OrderBy(pair => pair.Value.LastAccessUtc)
                        .FirstOrDefault();
                    if (evictable.Key is not null) {
                        _roots.Remove(evictable.Key);
                        evictable.Value.RefreshGate.Dispose();
                    }
                }

                state = new RootIndex();
                if (_roots.Count < MaximumCachedRoots)
                    _roots[canonicalRoot] = state;
            }

            state.LastAccessUtc = DateTime.UtcNow;
            Interlocked.Increment(ref state.ActiveOperations);
            return state;
        }
    }

    private static SourceIndexResult RefreshRoot(
        RootIndex state,
        string canonicalRoot,
        int maxFiles,
        CancellationToken cancellationToken) {
        var enumeration = EnumerateSourceFiles(canonicalRoot, maxFiles, cancellationToken);
        var previous = state.Files;
        var next = new Dictionary<string, IndexedSourceFile>(previous, StringComparer.OrdinalIgnoreCase);
        var activePaths = new HashSet<string>(enumeration.Files, StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>(enumeration.Warnings);
        var parsed = 0;
        var reused = 0;
        var parseErrors = 0;

        foreach (var file in enumeration.Files) {
            cancellationToken.ThrowIfCancellationRequested();
            SourceFileFingerprint fingerprint;
            try {
                fingerprint = ReadFingerprint(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) {
                next.Remove(file);
                parseErrors++;
                warnings.Add($"Could not read source file '{file}': {ex.Message}");
                continue;
            }

            if (previous.TryGetValue(file, out var cached) && cached.Fingerprint == fingerprint) {
                reused++;
                continue;
            }

            try {
                next[file] = ParseFile(file, fingerprint, cancellationToken);
                parsed++;
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) {
                next.Remove(file);
                parseErrors++;
                warnings.Add($"Could not parse source file '{file}': {ex.Message}");
            }
        }

        var removed = 0;
        if (enumeration.Complete) {
            foreach (var stalePath in previous.Keys.Where(path => !activePaths.Contains(path)).ToArray()) {
                if (next.Remove(stalePath))
                    removed++;
            }
        }

        state.Files = next;
        var types = enumeration.Files
            .Where(next.ContainsKey)
            .SelectMany(path => next[path].Types)
            .ToArray();
        return new SourceIndexResult(
            canonicalRoot,
            types,
            new SourceIndexSnapshot {
                Root = canonicalRoot,
                MaxFiles = maxFiles,
                Scanned = enumeration.Files.Count,
                Parsed = parsed,
                Reused = reused,
                Removed = removed,
                CachedFiles = next.Count,
                Truncated = enumeration.Truncated,
                ParseErrors = parseErrors,
                Warnings = warnings
            });
    }

    private static IndexedSourceFile ParseFile(
        string path,
        SourceFileFingerprint fingerprint,
        CancellationToken cancellationToken) {
        var text = File.ReadAllText(path);
        var tree = CSharpSyntaxTree.ParseText(text, path: path, cancellationToken: cancellationToken);
        var root = tree.GetCompilationUnitRoot(cancellationToken);
        var types = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Select(type => ParseType(path, type, cancellationToken))
            .ToArray();
        return new IndexedSourceFile(path, fingerprint, types);
    }

    private static IndexedSourceType ParseType(
        string path,
        ClassDeclarationSyntax type,
        CancellationToken cancellationToken) {
        var namespaceName = GetNamespace(type);
        var typeName = type.Identifier.ValueText;
        var fullyQualifiedType = string.IsNullOrWhiteSpace(namespaceName)
            ? typeName
            : $"{namespaceName}.{typeName}";
        var fields = new Dictionary<string, SourceLocationSnapshot>(StringComparer.Ordinal);
        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>()) {
            foreach (var variable in field.Declaration.Variables)
                fields[variable.Identifier.ValueText] = ToLocation(field.GetLocation(), path);
        }

        var initialization = new Dictionary<string, SourceLocationSnapshot>(StringComparer.Ordinal);
        foreach (var initializeMethod in type.Members
                     .OfType<MethodDeclarationSyntax>()
                     .Where(method => string.Equals(method.Identifier.ValueText, "InitializeComponent", StringComparison.Ordinal))) {
            foreach (var statement in initializeMethod.DescendantNodes().OfType<StatementSyntax>()) {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var identifier in statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
                    if (fields.ContainsKey(identifier.Identifier.ValueText))
                        initialization.TryAdd(identifier.Identifier.ValueText, ToLocation(statement.GetLocation(), path));
                }
            }
        }

        var events = new List<IndexedSourceEvent>();
        foreach (var assignment in type.DescendantNodes()
                     .OfType<AssignmentExpressionSyntax>()
                     .Where(item => item.IsKind(SyntaxKind.AddAssignmentExpression))) {
            cancellationToken.ThrowIfCancellationRequested();
            if (assignment.Left is not MemberAccessExpressionSyntax eventAccess ||
                assignment.Right.DescendantNodesAndSelf().OfType<AnonymousFunctionExpressionSyntax>().Any())
                continue;

            var controlName = GetControlName(eventAccess.Expression);
            if (string.IsNullOrWhiteSpace(controlName))
                continue;

            var handler = assignment.Right.DescendantNodesAndSelf()
                .OfType<MemberAccessExpressionSyntax>()
                .LastOrDefault(access => access.Expression is ThisExpressionSyntax)
                ?.Name.Identifier.ValueText;
            handler ??= assignment.Right.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .LastOrDefault()
                ?.Identifier.ValueText;
            if (string.IsNullOrWhiteSpace(handler))
                continue;

            var eventName = eventAccess.Name.Identifier.ValueText;
            events.Add(new IndexedSourceEvent(
                eventName,
                controlName,
                handler,
                ToLocation(assignment.GetLocation(), path)));
        }

        var methods = type.Members
            .OfType<MethodDeclarationSyntax>()
            .GroupBy(method => method.Identifier.ValueText, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => ToLocation(group.First().GetLocation(), path),
                StringComparer.Ordinal);

        return new IndexedSourceType(
            path,
            fullyQualifiedType,
            namespaceName,
            typeName,
            ToLocation(type.Identifier.GetLocation(), path),
            fields,
            initialization,
            events,
            methods);
    }

    private static string? GetControlName(ExpressionSyntax expression) => expression switch {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        _ => null
    };

    private static SourceFileFingerprint ReadFingerprint(string path) {
        var info = new FileInfo(path);
        return new SourceFileFingerprint(info.Length, info.LastWriteTimeUtc.Ticks);
    }

    private static SourceFileEnumeration EnumerateSourceFiles(
        string root,
        int maxFiles,
        CancellationToken cancellationToken) {
        var files = new List<string>(Math.Min(maxFiles, 256));
        var warnings = new List<string>();
        var truncated = false;
        var complete = true;
        try {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsExcluded(file))
                    continue;
                if (files.Count >= maxFiles) {
                    truncated = true;
                    complete = false;
                    break;
                }
                files.Add(Path.GetFullPath(file));
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) {
            complete = false;
            warnings.Add($"Could not enumerate source root '{root}': {ex.Message}");
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return new SourceFileEnumeration(files, truncated, complete, warnings);
    }

    private static bool IsExcluded(string path) {
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }

    private static string CanonicalizeRoot(string root) {
        var full = Path.GetFullPath(root);
        var canonical = Directory.Exists(full)
            ? new DirectoryInfo(full).FullName
            : full;
        var pathRoot = Path.GetPathRoot(canonical);
        return string.Equals(canonical, pathRoot, StringComparison.OrdinalIgnoreCase)
            ? canonical
            : canonical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static int NormalizeMaxFiles(int? requested) => requested is null
        ? DefaultMaxFiles
        : Math.Clamp(requested.Value, 1, MaximumMaxFiles);

    private static string GetNamespace(ClassDeclarationSyntax type) {
        var namespaces = type.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(item => item.Name.ToString())
            .Reverse()
            .ToArray();
        return string.Join(".", namespaces);
    }

    private static SourceLocationSnapshot ToLocation(Location location, string file) {
        var span = location.GetLineSpan();
        return new SourceLocationSnapshot {
            File = file,
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1,
            EndLine = span.EndLinePosition.Line + 1,
            EndColumn = span.EndLinePosition.Character + 1
        };
    }

    private sealed class RootIndex {
        public readonly SemaphoreSlim RefreshGate = new(1, 1);
        public Dictionary<string, IndexedSourceFile> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime LastAccessUtc { get; set; } = DateTime.UtcNow;
        public int ActiveOperations;
    }

    private readonly record struct SourceFileFingerprint(long Length, long LastWriteUtcTicks);

    private sealed record IndexedSourceFile(
        string Path,
        SourceFileFingerprint Fingerprint,
        IReadOnlyList<IndexedSourceType> Types);

    private sealed record SourceFileEnumeration(
        IReadOnlyList<string> Files,
        bool Truncated,
        bool Complete,
        IReadOnlyList<string> Warnings);
}

internal sealed record SourceIndexResult(
    string Root,
    IReadOnlyList<IndexedSourceType> Types,
    SourceIndexSnapshot Metadata);

internal sealed record IndexedSourceType(
    string Path,
    string FullyQualifiedName,
    string Namespace,
    string Name,
    SourceLocationSnapshot TypeLocation,
    IReadOnlyDictionary<string, SourceLocationSnapshot> Fields,
    IReadOnlyDictionary<string, SourceLocationSnapshot> Initialization,
    IReadOnlyList<IndexedSourceEvent> Events,
    IReadOnlyDictionary<string, SourceLocationSnapshot> Methods);

internal sealed record IndexedSourceEvent(
    string Event,
    string ControlName,
    string Method,
    SourceLocationSnapshot Location);