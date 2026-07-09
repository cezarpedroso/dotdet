using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Forge.Api.Analysis;

public sealed class SemanticAnalysisHelper
{
    public SemanticProjectContext? GetSemanticProject(SolutionAnalysisContext context, AnalyzedProject project)
    {
        return context.SemanticProjects.FirstOrDefault(candidate =>
            string.Equals(candidate.Project.FilePath, project.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<SemanticDocumentContext> GetSemanticDocuments(SolutionAnalysisContext context, AnalyzedProject project)
    {
        return GetSemanticProject(context, project)?.Documents
            ?? context.SemanticDocuments.Where(document =>
                string.Equals(document.Project.FilePath, project.FilePath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
    }

    public IReadOnlyList<SourceFileContext> GetSourceFiles(SolutionAnalysisContext context, AnalyzedProject project)
    {
        return context.SourceFiles.Where(file =>
            string.Equals(file.Project.FilePath, project.FilePath, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public IReadOnlyList<SemanticDocumentContext> GetStartupSemanticDocuments(
        SolutionAnalysisContext context,
        AnalyzedProject project)
    {
        return GetSemanticDocuments(context, project)
            .Where(document => IsStartupFile(document.FilePath))
            .ToArray();
    }

    public IReadOnlyList<SourceFileContext> GetStartupSourceFiles(
        SolutionAnalysisContext context,
        AnalyzedProject project)
    {
        return GetSourceFiles(context, project)
            .Where(file => IsStartupFile(file.FilePath))
            .ToArray();
    }

    public IReadOnlyList<string> GetProjectReferencePaths(
        SolutionAnalysisContext context,
        AnalyzedProject project)
    {
        var semanticProject = GetSemanticProject(context, project);
        return semanticProject is { ProjectReferencePaths.Count: > 0 }
            ? semanticProject.ProjectReferencePaths
            : project.ProjectReferencePaths;
    }

    public IReadOnlyList<AnalyzedProject> GetReferencedProjects(
        SolutionAnalysisContext context,
        AnalyzedProject project)
    {
        var projectByPath = context.Projects.ToDictionary(
            candidate => AnalyzerUtilities.NormalizePath(candidate.FilePath),
            StringComparer.OrdinalIgnoreCase);

        return GetProjectReferencePaths(context, project)
            .Where(projectByPath.ContainsKey)
            .Select(path => projectByPath[path])
            .ToArray();
    }

    public IReadOnlyList<string> GetReferencedAssemblyNames(
        SolutionAnalysisContext context,
        AnalyzedProject project)
    {
        return GetSemanticProject(context, project)?.ReferencedAssemblyNames ?? Array.Empty<string>();
    }

    public bool ProjectReferencesAssembly(
        SolutionAnalysisContext context,
        AnalyzedProject project,
        params string[] assemblyNameFragments)
    {
        return GetReferencedAssemblyNames(context, project).Any(assemblyName =>
            assemblyNameFragments.Any(fragment =>
                assemblyName.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    public bool SymbolBelongsToProject(
        SolutionAnalysisContext context,
        ISymbol? symbol,
        AnalyzedProject targetProject)
    {
        if (symbol?.ContainingAssembly is null)
        {
            return false;
        }

        var targetAssemblyName = GetSemanticProject(context, targetProject)?.AssemblyName ?? targetProject.Name;
        return string.Equals(symbol.ContainingAssembly.Name, targetAssemblyName, StringComparison.OrdinalIgnoreCase);
    }

    public bool TypeBelongsToProject(
        SolutionAnalysisContext context,
        ITypeSymbol? typeSymbol,
        AnalyzedProject targetProject)
    {
        return SymbolBelongsToProject(context, typeSymbol, targetProject);
    }

    public bool InheritsFrom(ITypeSymbol? typeSymbol, params string[] metadataNames)
    {
        for (var current = typeSymbol; current is not null; current = current.BaseType)
        {
            if (MatchesMetadataName(current, metadataNames))
            {
                return true;
            }
        }

        return false;
    }

    public bool ImplementsInterface(ITypeSymbol? typeSymbol, params string[] metadataNames)
    {
        return typeSymbol?.AllInterfaces.Any(@interface => MatchesMetadataName(@interface, metadataNames)) == true;
    }

    public bool IsNamedOrConstructedFrom(ITypeSymbol? typeSymbol, params string[] metadataNames)
    {
        if (typeSymbol is null)
        {
            return false;
        }

        if (MatchesMetadataName(typeSymbol, metadataNames))
        {
            return true;
        }

        return typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol
            && MatchesMetadataName(namedTypeSymbol.OriginalDefinition, metadataNames);
    }

    public IMethodSymbol? GetMethodSymbol(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        var symbol = semanticModel.GetSymbolInfo(invocation).Symbol
            ?? semanticModel.GetSymbolInfo(invocation.Expression).Symbol;

        return symbol as IMethodSymbol;
    }

    public InvocationMatch? FindFirstInvocation(
        IEnumerable<SemanticDocumentContext> documents,
        Func<SemanticDocumentContext, InvocationExpressionSyntax, IMethodSymbol?, bool> predicate)
    {
        foreach (var document in documents.OrderBy(candidate => candidate.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var invocation in document.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var methodSymbol = GetMethodSymbol(document.SemanticModel, invocation);
                if (!predicate(document, invocation, methodSymbol))
                {
                    continue;
                }

                return new InvocationMatch(
                    document.Project,
                    document.FilePath,
                    AnalyzerUtilities.GetLineNumber(invocation),
                    invocation,
                    methodSymbol);
            }
        }

        return null;
    }

    public bool HasInvocation(
        IEnumerable<SemanticDocumentContext> documents,
        Func<SemanticDocumentContext, InvocationExpressionSyntax, IMethodSymbol?, bool> predicate)
    {
        return FindFirstInvocation(documents, predicate) is not null;
    }

    public TypeReferenceMatch? FindFirstTypeReference(
        SolutionAnalysisContext context,
        AnalyzedProject project,
        Func<ITypeSymbol, bool> predicate)
    {
        foreach (var document in GetSemanticDocuments(context, project)
                     .OrderBy(candidate => candidate.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var typeSyntax in document.Root.DescendantNodes().OfType<TypeSyntax>())
            {
                var typeSymbol = document.SemanticModel.GetTypeInfo(typeSyntax).Type;
                if (typeSymbol is null || !predicate(typeSymbol))
                {
                    continue;
                }

                return new TypeReferenceMatch(
                    project,
                    document.FilePath,
                    AnalyzerUtilities.GetLineNumber(typeSyntax),
                    FormatTypeName(typeSymbol));
            }
        }

        return null;
    }

    public bool TryGetConstantString(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        out string value)
    {
        var constantValue = semanticModel.GetConstantValue(expression);
        if (constantValue is { HasValue: true, Value: string stringValue })
        {
            value = stringValue;
            return true;
        }

        if (expression is LiteralExpressionSyntax literal && literal.Token.Value is string literalValue)
        {
            value = literalValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public string FormatTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    public string GetTypeKey(ITypeSymbol typeSymbol)
    {
        return typeSymbol
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, StringComparison.Ordinal);
    }

    public IReadOnlyList<string> GetTypeKeys(ITypeSymbol typeSymbol)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            GetTypeKey(typeSymbol)
        };

        if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol)
        {
            keys.Add(GetTypeKey(namedTypeSymbol.OriginalDefinition));
        }

        return keys.ToArray();
    }

    private static bool MatchesMetadataName(ITypeSymbol typeSymbol, IEnumerable<string> metadataNames)
    {
        var fullyQualifiedName = typeSymbol
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, StringComparison.Ordinal);

        return metadataNames.Any(metadataName =>
            fullyQualifiedName.Equals(metadataName, StringComparison.Ordinal)
            || typeSymbol.Name.Equals(metadataName.Split('.').Last(), StringComparison.Ordinal));
    }

    private static bool IsStartupFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return fileName.Equals("Program.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Startup.cs", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record InvocationMatch(
    AnalyzedProject Project,
    string FilePath,
    int LineNumber,
    InvocationExpressionSyntax Invocation,
    IMethodSymbol? MethodSymbol);

public sealed record TypeReferenceMatch(
    AnalyzedProject Project,
    string FilePath,
    int LineNumber,
    string TypeDisplayName);
