using System.Text.Json.Serialization;

namespace Forge.Api.Models;

public sealed record AnalysisResult
{
    public string? AnalysisRunId { get; init; }

    public required string SolutionName { get; init; }

    public required DateTimeOffset AnalyzedAt { get; init; }

    public required int OverallScore { get; init; }

    public required CategoryScores CategoryScores { get; init; }

    public required IReadOnlyList<AnalysisIssue> Issues { get; init; }

    public required ProjectGraph ProjectGraph { get; init; }

    public IReadOnlyList<AnalysisSourceFile> SourceFiles { get; init; } = Array.Empty<AnalysisSourceFile>();

    public bool IsHistoricalSnapshot { get; init; }

    public bool SourcePreviewAvailable { get; init; } = true;

    public string? SourcePreviewUnavailableReason { get; init; }

    public bool SourcePreviewCapped { get; init; }

    public string? SourcePreviewCappedReason { get; init; }

    public int SourcePreviewIncludedFileCount { get; init; }

    public int SourcePreviewOmittedFileCount { get; init; }

    public long SourcePreviewIncludedBytes { get; init; }

    public int SourcePreviewFileCountLimit { get; init; }

    public long SourcePreviewByteLimit { get; init; }

    public string AnalysisFidelity { get; init; } = "Roslyn Semantic Analysis";

    public bool SemanticAnalysisSkipped { get; init; }

    public string? SemanticAnalysisSkippedReason { get; init; }

    public ArchitectureMap? ArchitectureMap { get; init; }

    public EngineeringAssessmentSummary? EngineeringAssessment { get; init; }

    public string? SolutionPath { get; init; }

    public string? RepositoryRoot { get; init; }

    public string? SuppressionFilePath { get; init; }

    public int SuppressionCount { get; init; }
}

public sealed record AnalysisSourceFile
{
    public required string ProjectName { get; init; }

    public required string FilePath { get; init; }

    public required string RelativePath { get; init; }

    public required string Content { get; init; }

    public required string Language { get; init; }
}

public sealed record CategoryScores
{
    public required int Architecture { get; init; }

    public required int DependencyInjection { get; init; }

    public required int EfCore { get; init; }

    public required int Security { get; init; }

    public required int ApiReadiness { get; init; }
}

public sealed record AnalysisIssue
{
    public required string Id { get; init; }

    public string? RuleId { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required IssueSeverity Severity { get; init; }

    public required string Category { get; init; }

    public string? ProjectName { get; init; }

    public string? FilePath { get; init; }

    public int? LineNumber { get; init; }

    public required string Recommendation { get; init; }

    public IssueConfidence? Confidence { get; init; }

    public string? DetectionMethod { get; init; }

    public string? ProblemSummary { get; init; }

    public string? WhyDetected { get; init; }

    public string? WhyItMatters { get; init; }

    public string? RecommendedPattern { get; init; }

    public string? SuggestedImplementation { get; init; }

    public IReadOnlyList<AnalysisDocumentationLink>? DocumentationLinks { get; init; }

    public IReadOnlyList<string>? RelatedFindingIds { get; init; }

    public IReadOnlyList<AnalysisEvidence>? Evidence { get; init; }

    public string? RootCauseKey { get; init; }

    public string? SuggestedSnippet { get; init; }

    public string? GoodExample { get; init; }

    public string? BadExample { get; init; }

    public FindingSuppression? Suppression { get; init; }
}

public sealed record FindingSuppression
{
    public required string Id { get; init; }

    public required string RuleId { get; init; }

    public string? File { get; init; }

    public string? Project { get; init; }

    public required string Reason { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset CreatedDate { get; init; }

    public DateTimeOffset? Expiration { get; init; }

    public required bool IsExpired { get; init; }
}

public sealed record AnalysisDocumentationLink
{
    public required string Label { get; init; }

    public required string Href { get; init; }
}

public sealed record AnalysisEvidence
{
    public required string Label { get; init; }

    public required string Detail { get; init; }

    public string? FilePath { get; init; }

    public int? LineNumber { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IssueConfidence
{
    Low,
    Medium,
    High
}

public sealed record ProjectGraph
{
    public required IReadOnlyList<ProjectNode> Projects { get; init; }

    public required IReadOnlyList<ProjectDependency> Dependencies { get; init; }
}

public sealed record ProjectNode
{
    public required string Name { get; init; }

    public required string FilePath { get; init; }

    public string? LogicalLayer { get; init; }

    public bool IsTestProject { get; init; }

    public bool IsAspNetCoreEntryPoint { get; init; }
}

public sealed record ProjectDependency
{
    public required string SourceProjectName { get; init; }

    public required string TargetProjectName { get; init; }
}

public sealed record ArchitectureMap
{
    public required IReadOnlyList<ArchitectureLayer> Layers { get; init; }

    public required IReadOnlyList<ArchitectureMapProject> Projects { get; init; }

    public required IReadOnlyList<ArchitectureMapDependency> Dependencies { get; init; }

    public required IReadOnlyList<ArchitectureMapViolation> Violations { get; init; }
}

public sealed record ArchitectureLayer
{
    public required string Name { get; init; }

    public required int Order { get; init; }

    public required IReadOnlyList<string> ProjectNames { get; init; }
}

public sealed record ArchitectureMapProject
{
    public required string Name { get; init; }

    public required string FilePath { get; init; }

    public required string Layer { get; init; }

    public required string NamespaceRoot { get; init; }

    public required int IssueCount { get; init; }

    public required int CriticalOrErrorCount { get; init; }
}

public sealed record ArchitectureMapDependency
{
    public required string SourceProjectName { get; init; }

    public required string TargetProjectName { get; init; }

    public required string SourceLayer { get; init; }

    public required string TargetLayer { get; init; }

    public required string Direction { get; init; }

    public required bool IsViolation { get; init; }

    public string? RuleId { get; init; }

    public string? Reason { get; init; }

    public string? RelatedFindingId { get; init; }
}

public sealed record ArchitectureMapViolation
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string RuleId { get; init; }

    public string? SourceProjectName { get; init; }

    public string? TargetProjectName { get; init; }

    public string? RelatedFindingId { get; init; }
}

public sealed record EngineeringAssessmentSummary
{
    public required string OverallProductionReadiness { get; init; }

    public required string ScoreExplanation { get; init; }

    public required IReadOnlyList<string> StrongAreas { get; init; }

    public required IReadOnlyList<string> HighestRisks { get; init; }

    public required IReadOnlyList<string> ArchitecturalObservations { get; init; }

    public required IReadOnlyList<string> SecurityObservations { get; init; }

    public required IReadOnlyList<string> ApiReadinessObservations { get; init; }

    public required IReadOnlyList<string> MaintainabilityObservations { get; init; }

    public required IReadOnlyList<string> RecommendedPriorities { get; init; }
}

public sealed record RuleDocumentation
{
    public required string RuleId { get; init; }

    public required string Title { get; init; }

    public required string Category { get; init; }

    public required IssueSeverity Severity { get; init; }

    public required string DetectionMethod { get; init; }

    public required IssueConfidence Confidence { get; init; }

    public required string ConfidenceExplanation { get; init; }

    public required string ProblemSummary { get; init; }

    public required string WhyItMatters { get; init; }

    public required string DetectionLogic { get; init; }

    public required string RecommendedPattern { get; init; }

    public required string SuggestedImplementation { get; init; }

    public string? GoodExample { get; init; }

    public string? BadExample { get; init; }

    public string? SuggestedCodeSnippet { get; init; }

    public required IReadOnlyList<AnalysisDocumentationLink> DocumentationLinks { get; init; }

    public required string FalsePositiveGuidance { get; init; }

    public required IReadOnlyList<string> RelatedRules { get; init; }
}

public sealed record DotDetSuppressionFile
{
    public int Version { get; init; } = 1;

    public string? RepositoryId { get; init; }

    public IReadOnlyList<RepositorySuppression> Suppressions { get; init; } = Array.Empty<RepositorySuppression>();
}

public sealed record RepositorySuppression
{
    public required string Id { get; init; }

    public required string RuleId { get; init; }

    public string? File { get; init; }

    public string? Project { get; init; }

    public required string Reason { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset CreatedDate { get; init; }

    public DateTimeOffset? Expiration { get; init; }
}
