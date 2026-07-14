using Forge.Api.Models;

namespace Forge.Api.Services;

public static class AnalysisResultSanitizer
{
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
            SolutionPath = null,
            RepositoryRoot = null,
            SuppressionFilePath = null,
            SourceFiles = Array.Empty<AnalysisSourceFile>(),
            Issues = RelativizeIssues(result.Issues, root),
            ProjectGraph = RelativizeProjectGraph(result.ProjectGraph, root),
            ArchitectureMap = RelativizeArchitectureMap(result.ArchitectureMap, root)
        };
    }

    public static AnalysisResult CreateLiveRepositoryResponse(AnalysisResult result)
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
            ArchitectureMap = RelativizeArchitectureMap(result.ArchitectureMap, root)
        };
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
            Evidence = issue.Evidence?.Select(evidence => evidence with
            {
                FilePath = ToReportPath(evidence.FilePath, root)
            }).ToArray()
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
                }).ToArray()
            };
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
            return normalizedPath;
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
}
