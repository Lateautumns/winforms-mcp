using System.Diagnostics;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Server.Runtime;

/// <summary>
/// Maps a managed control name to Designer and code-behind source locations.
/// The scanner is bounded and read-only; it never edits project files.
/// </summary>
internal sealed class SourceMappingService {
    private static readonly Regex TypeNamePattern = new(
        @"(?:global::)?(?<type>[A-Za-z_][A-Za-z0-9_.]*)",
        RegexOptions.Compiled);
    private const int MaxSourceFiles = 5000;

    public Task<SourceMappingSnapshot> MapAsync(
        int processId,
        ControlIdentity control,
        string? sourceRoot,
        CancellationToken cancellationToken) =>
        Task.Run(() => Map(processId, control, sourceRoot, cancellationToken), cancellationToken);

    private SourceMappingSnapshot Map(
        int processId,
        ControlIdentity control,
        string? sourceRoot,
        CancellationToken cancellationToken) {
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

        var files = EnumerateSourceFiles(root);
        var candidates = new List<(string Path, SyntaxTree Tree, CompilationUnitSyntax Unit, ClassDeclarationSyntax Type)>();
        foreach (var file in files) {
            cancellationToken.ThrowIfCancellationRequested();
            string text;
            try {
                text = File.ReadAllText(file);
            }
            catch {
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(text, path: file, cancellationToken: cancellationToken);
            var unit = tree.GetCompilationUnitRoot(cancellationToken);
            foreach (var type in unit.DescendantNodes().OfType<ClassDeclarationSyntax>()) {
                var namespaceName = GetNamespace(type);
                var candidateType = string.IsNullOrWhiteSpace(namespaceName)
                    ? type.Identifier.ValueText
                    : $"{namespaceName}.{type.Identifier.ValueText}";
                if (!string.Equals(type.Identifier.ValueText, ownerTypeName, StringComparison.Ordinal) ||
                    (ownerType.Contains('.', StringComparison.Ordinal) &&
                     !string.Equals(candidateType, ownerType, StringComparison.Ordinal)))
                    continue;
                candidates.Add((file, tree, unit, type));
                if (string.IsNullOrWhiteSpace(result.Namespace))
                    result.Namespace = namespaceName;
                if (string.IsNullOrWhiteSpace(result.Type))
                    result.Type = ownerTypeName;
                if (string.IsNullOrWhiteSpace(result.FullyQualifiedType))
                    result.FullyQualifiedType = string.IsNullOrWhiteSpace(namespaceName)
                        ? ownerTypeName
                        : $"{namespaceName}.{ownerTypeName}";
            }
        }

        if (candidates.Count == 0) {
            result.Warnings.Add($"No source declaration for owning Form '{ownerTypeName}' was found under '{root}'.");
            return result;
        }

        var designer = candidates.FirstOrDefault(candidate =>
            candidate.Path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));
        var codeBehind = candidates.FirstOrDefault(candidate =>
            !candidate.Path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(designer.Path)) {
            result.Designer = ToLocation(designer.Type.Identifier.GetLocation(), designer.Path);
            result.Declaration = FindFieldLocation(designer, control.Name);
            result.Initialization = FindInitializationLocation(designer, control.Name);
            FindEvents(designer, control.Name, result, cancellationToken);
        }
        else {
            result.Warnings.Add("A .Designer.cs partial class was not found.");
        }

        if (!string.IsNullOrWhiteSpace(codeBehind.Path)) {
            result.CodeBehindFile = codeBehind.Path;
            FindEventMethods(codeBehind, result, cancellationToken);
        }
        else {
            result.Warnings.Add("A non-Designer code-behind file was not found.");
        }

        return result;
    }

    private static SourceLocationSnapshot? FindFieldLocation(
        (string Path, SyntaxTree Tree, CompilationUnitSyntax Unit, ClassDeclarationSyntax Type) candidate,
        string controlName) {
        var field = candidate.Type.Members
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(field => field.Declaration.Variables.Any(variable =>
                string.Equals(variable.Identifier.ValueText, controlName, StringComparison.Ordinal)));
        return field is null ? null : ToLocation(field.GetLocation(), candidate.Path);
    }

    private static SourceLocationSnapshot? FindInitializationLocation(
        (string Path, SyntaxTree Tree, CompilationUnitSyntax Unit, ClassDeclarationSyntax Type) candidate,
        string controlName) {
        var initializeMethod = candidate.Type.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(method => string.Equals(method.Identifier.ValueText, "InitializeComponent", StringComparison.Ordinal));
        if (initializeMethod is null)
            return null;

        var statement = initializeMethod.DescendantNodes()
            .OfType<StatementSyntax>()
            .FirstOrDefault(statement => statement.ToString().Contains(controlName, StringComparison.Ordinal));
        return statement is null ? null : ToLocation(statement.GetLocation(), candidate.Path);
    }

    private static void FindEvents(
        (string Path, SyntaxTree Tree, CompilationUnitSyntax Unit, ClassDeclarationSyntax Type) candidate,
        string controlName,
        SourceMappingSnapshot result,
        CancellationToken cancellationToken) {
        foreach (var assignment in candidate.Type.DescendantNodes()
                     .OfType<AssignmentExpressionSyntax>()
                     .Where(item => item.IsKind(SyntaxKind.AddAssignmentExpression))) {
            cancellationToken.ThrowIfCancellationRequested();
            if (assignment.Left is not MemberAccessExpressionSyntax eventAccess ||
                !IsControlExpression(eventAccess.Expression, controlName))
                continue;

            if (assignment.Right.DescendantNodesAndSelf().OfType<AnonymousFunctionExpressionSyntax>().Any())
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
            var location = ToLocation(assignment.GetLocation(), candidate.Path);
            result.Events[eventName] = new EventHandlerSnapshot {
                Event = eventName,
                Method = handler,
                File = candidate.Path,
                Line = location.Line
            };
        }
    }

    private static void FindEventMethods(
        (string Path, SyntaxTree Tree, CompilationUnitSyntax Unit, ClassDeclarationSyntax Type) candidate,
        SourceMappingSnapshot result,
        CancellationToken cancellationToken) {
        foreach (var mapping in result.Events.Values) {
            cancellationToken.ThrowIfCancellationRequested();
            var method = candidate.Type.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(item => string.Equals(item.Identifier.ValueText, mapping.Method, StringComparison.Ordinal));
            if (method is null) {
                result.Warnings.Add($"Event handler '{mapping.Method}' was not found in '{candidate.Path}'.");
                continue;
            }

            var location = ToLocation(method.GetLocation(), candidate.Path);
            mapping.File = candidate.Path;
            mapping.Line = location.Line;
            mapping.FullyQualifiedSymbol = string.IsNullOrWhiteSpace(result.FullyQualifiedType)
                ? mapping.Method
                : $"{result.FullyQualifiedType}.{mapping.Method}";
        }
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
        catch {
            return null;
        }
    }

    private static IReadOnlyList<string> EnumerateSourceFiles(string root) {
        try {
            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Take(MaxSourceFiles)
                .ToArray();
        }
        catch (Exception) {
            return Array.Empty<string>();
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

    private static bool IsControlExpression(ExpressionSyntax expression, string controlName) => expression switch {
        IdentifierNameSyntax identifier => string.Equals(identifier.Identifier.ValueText, controlName, StringComparison.Ordinal),
        MemberAccessExpressionSyntax member => string.Equals(member.Name.Identifier.ValueText, controlName, StringComparison.Ordinal),
        _ => false
    };

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
}
