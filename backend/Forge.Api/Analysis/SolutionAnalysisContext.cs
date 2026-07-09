using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Forge.Api.Analysis;

public sealed record SolutionAnalysisContext
{
    public required string SolutionPath { get; init; }

    public required string SolutionName { get; init; }

    public required string RootDirectory { get; init; }

    public required IReadOnlyList<AnalyzedProject> Projects { get; init; }

    public required IReadOnlyList<SourceFileContext> SourceFiles { get; init; }

    public required IReadOnlyList<SemanticProjectContext> SemanticProjects { get; init; }

    public required IReadOnlyList<SemanticDocumentContext> SemanticDocuments { get; init; }

    public required IReadOnlyList<string> AppSettingsFiles { get; init; }
}

public sealed record AnalyzedProject
{
    public required string Name { get; init; }

    public required string FilePath { get; init; }

    public string? AssemblyName { get; init; }

    public required string DirectoryPath { get; init; }

    public string? Sdk { get; init; }

    public string? TargetFramework { get; init; }

    public required bool IsWebProject { get; init; }

    public required bool IsAspNetCoreEntryPoint { get; init; }

    public required bool IsTestProject { get; init; }

    public required string LogicalLayer { get; init; }

    public required bool LoadedWithMsBuild { get; init; }

    public required IReadOnlySet<string> PackageReferences { get; init; }

    public required IReadOnlyList<string> ProjectReferencePaths { get; init; }

    public required IReadOnlyList<string> TransitiveProjectReferencePaths { get; init; }
}

public sealed record SourceFileContext
{
    public required AnalyzedProject Project { get; init; }

    public required string FilePath { get; init; }

    public required string Text { get; init; }

    public required CompilationUnitSyntax Root { get; init; }
}

public sealed record SemanticDocumentContext
{
    public required AnalyzedProject Project { get; init; }

    public required string FilePath { get; init; }

    public required CompilationUnitSyntax Root { get; init; }

    public required SemanticModel SemanticModel { get; init; }
}

public sealed record SemanticProjectContext
{
    public required AnalyzedProject Project { get; init; }

    public required string AssemblyName { get; init; }

    public required Compilation Compilation { get; init; }

    public required IReadOnlyList<SemanticDocumentContext> Documents { get; init; }

    public required IReadOnlyList<string> ProjectReferencePaths { get; init; }

    public required IReadOnlyList<string> ProjectReferenceNames { get; init; }

    public required IReadOnlyList<string> ReferencedAssemblyNames { get; init; }
}
