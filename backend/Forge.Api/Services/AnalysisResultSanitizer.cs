using Forge.Api.Models;
using System.Text.RegularExpressions;

namespace Forge.Api.Services;

public static class AnalysisResultSanitizer
{
    private static readonly Regex WindowsAbsolutePathRegex = new(
        @"(?<![A-Za-z0-9])(?:[A-Za-z]:[\\/])[^\r\n\t\""<>|]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UnixAbsolutePathRegex = new(
        @"(?<![:A-Za-z0-9])/(?:tmp|var|home|Users|private|mnt)/[^\r\n\t\""<>|]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public const string HistoricalSourcePreviewUnavailableReason =
        "Source preview is not stored for saved reports. DotDet preserves findings, evidence, scores, and exports without retaining full repository source code. Re-run the analysis to inspect source preview again.";

    public static AnalysisResult CreateHistorySnapshot(AnalysisResult result)
    {
        var root = result.RepositoryRoot;

        return result with
        {
            IsHistoricalSnapshot = true,
            SourcePreviewAvailable = false,
            SourcePreviewUnavailableReason = HistoricalSourcePreviewUnavailableReason,
            SourcePreviewCapped = false,
            SourcePreviewCappedReason = null,
            SourcePreviewIncludedFileCount = 0,
            SourcePreviewOmittedFileCount = 0,
            SourcePreviewIncludedBytes = 0,
            SolutionPath = null,
            RepositoryRoot = null,
            SuppressionFilePath = null,
            SourceFiles = Array.Empty<AnalysisSourceFile>(),
            Issues = RelativizeIssues(result.Issues, root),
            ProjectGraph = RelativizeProjectGraph(result.ProjectGraph, root),
            ArchitectureMap = RelativizeArchitectureMap(result.ArchitectureMap, root),
            EngineeringAssessment = RelativizeEngineeringAssessment(result.EngineeringAssessment, root)
        };
    }

    public static AnalysisResult CreateLiveResponse(AnalysisResult result)
    {
        var root = result.RepositoryRoot;
        var sourceFiles = result.SourceFiles
            .Select(sourceFile => RelativizeSourceFile(sourceFile, root))
            .ToArray();

        return result with
        {
            IsHistoricalSnapshot = false,
            SourcePreviewAvailable = sourceFiles.Length > 0,
            SourcePreviewUnavailableReason = sourceFiles.Length > 0
                ? null
                : "Source preview is unavailable for this live analysis result.",
            SolutionPath = null,
            RepositoryRoot = null,
            SuppressionFilePath = null,
            SourceFiles = sourceFiles,
            Issues = RelativizeIssues(result.Issues, root),
            ProjectGraph = RelativizeProjectGraph(result.ProjectGraph, root),
            ArchitectureMap = RelativizeArchitectureMap(result.ArchitectureMap, root),
            EngineeringAssessment = RelativizeEngineeringAssessment(result.EngineeringAssessment, root)
        };
    }

    public static AnalysisResult CreateLiveRepositoryResponse(AnalysisResult result)
    {
        return CreateLiveResponse(result);
    }

    private static AnalysisSourceFile RelativizeSourceFile(AnalysisSourceFile sourceFile, string? root)
    {
        var relativePath = ToReportPath(sourceFile.FilePath, root)
            ?? NormalizeDisplayPath(sourceFile.RelativePath);

        return sourceFile with
        {
            FilePath = relativePath,
            RelativePath = relativePath
        };
    }

    private static IReadOnlyList<AnalysisIssue> RelativizeIssues(IReadOnlyList<AnalysisIssue> issues, string? root)
    {
        return issues.Select(issue => issue with
        {
            FilePath = ToReportPath(issue.FilePath, root),
            Description = SanitizeRequiredText(issue.Description, root),
            Recommendation = SanitizeRequiredText(issue.Recommendation, root),
            ProblemSummary = SanitizeText(issue.ProblemSummary, root),
            WhyDetected = SanitizeText(issue.WhyDetected, root),
            WhyItMatters = SanitizeText(issue.WhyItMatters, root),
            RecommendedPattern = SanitizeText(issue.RecommendedPattern, root),
            SuggestedImplementation = SanitizeText(issue.SuggestedImplementation, root),
            Evidence = issue.Evidence?.Select(evidence => evidence with
            {
                Label = SanitizeRequiredText(evidence.Label, root),
                Detail = SanitizeRequiredText(evidence.Detail, root),
                FilePath = ToReportPath(evidence.FilePath, root)
            }).ToArray(),
            Suppression = issue.Suppression is null
                ? null
                : issue.Suppression with { File = ToReportPath(issue.Suppression.File, root) }
        }).ToArray();
    }

    private static ProjectGraph RelativizeProjectGraph(ProjectGraph projectGraph, string? root)
    {
        return projectGraph with
        {
            Projects = projectGraph.Projects.Select(project => project with
            {
                FilePath = ToReportPath(project.FilePath, root) ?? project.FilePath
            }).ToArray()
        };
    }

    private static ArchitectureMap? RelativizeArchitectureMap(ArchitectureMap? architectureMap, string? root)
    {
        return architectureMap is null
            ? null
            : architectureMap with
            {
                Projects = architectureMap.Projects.Select(project => project with
                {
                    FilePath = ToReportPath(project.FilePath, root) ?? project.FilePath
                }).ToArray(),
                Dependencies = architectureMap.Dependencies.Select(dependency => dependency with
                {
                    Reason = SanitizeText(dependency.Reason, root)
                }).ToArray(),
                Violations = architectureMap.Violations.Select(violation => violation with
                {
                    Description = SanitizeRequiredText(violation.Description, root)
                }).ToArray()
            };
    }

    private static EngineeringAssessmentSummary? RelativizeEngineeringAssessment(
        EngineeringAssessmentSummary? assessment,
        string? root)
    {
        if (assessment is null)
        {
            return null;
        }

        return assessment with
        {
            OverallProductionReadiness = SanitizeRequiredText(assessment.OverallProductionReadiness, root),
            ScoreExplanation = SanitizeRequiredText(assessment.ScoreExplanation, root),
            StrongAreas = SanitizeTextList(assessment.StrongAreas, root),
            HighestRisks = SanitizeTextList(assessment.HighestRisks, root),
            ArchitecturalObservations = SanitizeTextList(assessment.ArchitecturalObservations, root),
            SecurityObservations = SanitizeTextList(assessment.SecurityObservations, root),
            ApiReadinessObservations = SanitizeTextList(assessment.ApiReadinessObservations, root),
            MaintainabilityObservations = SanitizeTextList(assessment.MaintainabilityObservations, root),
            RecommendedPriorities = SanitizeTextList(assessment.RecommendedPriorities, root)
        };
    }

    private static IReadOnlyList<string> SanitizeTextList(IReadOnlyList<string> values, string? root)
    {
        return values.Select(value => SanitizeRequiredText(value, root)).ToArray();
    }

    private static string? ToReportPath(string? filePath, string? root)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        var normalizedPath = NormalizeDisplayPath(filePath);
        if (!Path.IsPathRooted(filePath))
        {
            return normalizedPath.Split('/').Any(part => part == "..")
                ? Path.GetFileName(normalizedPath)
                : normalizedPath.TrimStart('.', '/');
        }

        if (string.IsNullOrWhiteSpace(root))
        {
            return Path.GetFileName(filePath);
        }

        var normalizedRoot = NormalizeDisplayPath(root).TrimEnd('/') + "/";
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            ? normalizedPath[normalizedRoot.Length..]
            : Path.GetFileName(filePath);
    }

    private static string NormalizeDisplayPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string? SanitizeText(string? value, string? root)
    {
        if (value is null)
        {
            return null;
        }

        var sanitized = value;
        if (!string.IsNullOrWhiteSpace(root))
        {
            sanitized = sanitized
                .Replace(root.TrimEnd('\\', '/'), string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(NormalizeDisplayPath(root).TrimEnd('/'), string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        sanitized = WindowsAbsolutePathRegex.Replace(sanitized, match => GetSafePathTail(match.Value));
        sanitized = UnixAbsolutePathRegex.Replace(sanitized, match => GetSafePathTail(match.Value));
        return sanitized;
    }

    private static string SanitizeRequiredText(string value, string? root)
    {
        return SanitizeText(value, root) ?? string.Empty;
    }

    private static string GetSafePathTail(string path)
    {
        var normalized = NormalizeDisplayPath(path).TrimEnd('.', ',', ';', ':', ')', ']');
        return normalized.Split('/').LastOrDefault(part => !string.IsNullOrWhiteSpace(part)) ?? "file";
    }
}
