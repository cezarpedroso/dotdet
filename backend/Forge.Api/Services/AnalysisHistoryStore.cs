using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Api.Models;

namespace Forge.Api.Services;

public sealed class AnalysisHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string storePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    static AnalysisHistoryStore()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public AnalysisHistoryStore(IWebHostEnvironment environment)
    {
        storePath = Path.Combine(environment.ContentRootPath, "Data", "dotdet-analysis-history.json");
    }

    public AnalysisHistoryStore(string storePath)
    {
        this.storePath = storePath;
    }

    public async Task<AnalysisHistoryRun> SaveAsync(
        string userId,
        AnalysisResult result,
        string sourceType,
        string sourceLabel,
        string? sourceUrl = null,
        string? githubOwner = null,
        string? githubRepo = null,
        string? gitRef = null,
        string? repositoryVisibility = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid().ToString("N");
        var snapshot = AnalysisResultSanitizer.CreateHistorySnapshot(result) with { AnalysisRunId = runId };
        var run = new AnalysisHistoryRun
        {
            Id = runId,
            UserId = userId,
            SolutionName = result.SolutionName,
            SourceType = sourceType,
            SourceLabel = sourceLabel,
            SourceUrl = sourceUrl,
            GitHubOwner = githubOwner,
            GitHubRepo = githubRepo,
            GitRef = gitRef,
            RepositoryVisibility = repositoryVisibility,
            Score = result.OverallScore,
            Grade = GetGrade(result.OverallScore),
            Status = GetReadinessStatus(result),
            OpenFindingCount = result.Issues.Count(issue => issue.Suppression is not { IsExpired: false }),
            TotalFindingCount = result.Issues.Count,
            CreatedAt = now,
            CompletedAt = now,
            ReportSnapshot = snapshot
        };

        await gate.WaitAsync(cancellationToken);
        try
        {
            var runs = await LoadUnsafeAsync(cancellationToken);
            runs.Add(run);
            runs.Sort((left, right) => right.CompletedAt.CompareTo(left.CompletedAt));
            await SaveUnsafeAsync(runs, cancellationToken);
        }
        finally
        {
            gate.Release();
        }

        return run;
    }

    public async Task<RepositorySuppression?> CreateSuppressionAsync(
        string userId,
        string analysisRunId,
        string findingId,
        string? reason,
        string? status,
        DateTimeOffset? expiration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(findingId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var runs = await LoadUnsafeAsync(cancellationToken);
            var runIndex = runs.FindIndex(run => run.UserId == userId && run.Id == analysisRunId);
            if (runIndex < 0)
            {
                return null;
            }

            var run = runs[runIndex];
            var issue = run.ReportSnapshot.Issues.FirstOrDefault(candidate =>
                candidate.Id.Equals(findingId, StringComparison.OrdinalIgnoreCase));
            if (issue is null)
            {
                return null;
            }

            var suppression = SuppressionService.CreateForIssue(issue, reason, status, expiration);
            var updatedIssues = run.ReportSnapshot.Issues
                .Select(candidate => candidate.Id.Equals(issue.Id, StringComparison.OrdinalIgnoreCase)
                    ? SuppressionService.ApplyToIssue(candidate, suppression)
                    : candidate)
                .ToArray();
            var updatedSnapshot = RecalculateSnapshot(run.ReportSnapshot with
            {
                Issues = updatedIssues,
                SuppressionCount = updatedIssues.Count(candidate => candidate.Suppression is not null)
            });

            runs[runIndex] = run with
            {
                ReportSnapshot = updatedSnapshot,
                Score = updatedSnapshot.OverallScore,
                Grade = GetGrade(updatedSnapshot.OverallScore),
                OpenFindingCount = CountOpenFindings(updatedSnapshot),
                Status = GetReadinessStatus(updatedSnapshot)
            };

            await SaveUnsafeAsync(runs, cancellationToken);
            return suppression;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> RemoveSuppressionAsync(
        string userId,
        string analysisRunId,
        string suppressionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(suppressionId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var runs = await LoadUnsafeAsync(cancellationToken);
            var runIndex = runs.FindIndex(run => run.UserId == userId && run.Id == analysisRunId);
            if (runIndex < 0)
            {
                return false;
            }

            var run = runs[runIndex];
            var removed = false;
            var updatedIssues = run.ReportSnapshot.Issues
                .Select(issue =>
                {
                    if (issue.Suppression?.Id.Equals(suppressionId, StringComparison.OrdinalIgnoreCase) != true)
                    {
                        return issue;
                    }

                    removed = true;
                    return issue with { Suppression = null };
                })
                .ToArray();

            if (!removed)
            {
                return false;
            }

            var updatedSnapshot = RecalculateSnapshot(run.ReportSnapshot with
            {
                Issues = updatedIssues,
                SuppressionCount = updatedIssues.Count(candidate => candidate.Suppression is not null)
            });

            runs[runIndex] = run with
            {
                ReportSnapshot = updatedSnapshot,
                Score = updatedSnapshot.OverallScore,
                Grade = GetGrade(updatedSnapshot.OverallScore),
                OpenFindingCount = CountOpenFindings(updatedSnapshot),
                Status = GetReadinessStatus(updatedSnapshot)
            };

            await SaveUnsafeAsync(runs, cancellationToken);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<AnalysisHistorySummary>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var runs = await LoadUnsafeAsync(cancellationToken);
            return runs
                .Where(run => run.UserId == userId)
                .OrderByDescending(run => run.CompletedAt)
                .Select(ToSummary)
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AnalysisHistoryDetail?> GetAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var runs = await LoadUnsafeAsync(cancellationToken);
            var run = runs.FirstOrDefault(candidate => candidate.UserId == userId && candidate.Id == id);
            return run is null
                ? null
                : new AnalysisHistoryDetail
                {
                    Summary = ToSummary(run),
                    Result = run.ReportSnapshot
                };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var runs = await LoadUnsafeAsync(cancellationToken);
            var removed = runs.RemoveAll(run => run.UserId == userId && run.Id == id) > 0;
            if (removed)
            {
                await SaveUnsafeAsync(runs, cancellationToken);
            }

            return removed;
        }
        finally
        {
            gate.Release();
        }
    }

    private static AnalysisHistorySummary ToSummary(AnalysisHistoryRun run)
    {
        return new AnalysisHistorySummary
        {
            Id = run.Id,
            SolutionName = run.SolutionName,
            SourceType = run.SourceType,
            SourceLabel = run.SourceLabel,
            SourceUrl = run.SourceUrl,
            GitHubOwner = run.GitHubOwner,
            GitHubRepo = run.GitHubRepo,
            GitRef = run.GitRef,
            RepositoryVisibility = run.RepositoryVisibility,
            Score = run.Score,
            Grade = run.Grade,
            Status = run.Status,
            OpenFindingCount = run.OpenFindingCount,
            TotalFindingCount = run.TotalFindingCount,
            CreatedAt = run.CreatedAt,
            CompletedAt = run.CompletedAt,
            CanRerun = run.SourceType == AnalysisSourceTypes.SampleProject
        };
    }

    private async Task<List<AnalysisHistoryRun>> LoadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(storePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(storePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return (JsonSerializer.Deserialize<List<AnalysisHistoryRun>>(json, JsonOptions) ?? [])
            .Select(SanitizeLegacyRun)
            .ToList();
    }

    private static AnalysisHistoryRun SanitizeLegacyRun(AnalysisHistoryRun run)
    {
        return run with
        {
            ReportSnapshot = AnalysisResultSanitizer.CreateHistorySnapshot(run.ReportSnapshot)
        };
    }

    private async Task SaveUnsafeAsync(List<AnalysisHistoryRun> runs, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(storePath) ?? ".");
        await File.WriteAllTextAsync(storePath, JsonSerializer.Serialize(runs, JsonOptions), cancellationToken);
    }

    private static string GetGrade(int score)
    {
        if (score >= 97) return "A+";
        if (score >= 93) return "A";
        if (score >= 90) return "A-";
        if (score >= 87) return "B+";
        if (score >= 83) return "B";
        if (score >= 80) return "B-";
        if (score >= 70) return "C";
        if (score >= 60) return "D";
        return "F";
    }

    private static string GetReadinessStatus(AnalysisResult result)
    {
        var activeIssues = result.Issues.Where(issue => issue.Suppression is not { IsExpired: false }).ToArray();
        if (result.OverallScore < 50)
        {
            return "Not Ready";
        }

        return activeIssues.Any(issue => issue.Severity is IssueSeverity.Critical or IssueSeverity.Error or IssueSeverity.Warning)
            || result.OverallScore < 85
                ? "Needs Review"
                : "Ready";
    }

    private static int CountOpenFindings(AnalysisResult result)
    {
        return result.Issues.Count(issue => issue.Suppression is not { IsExpired: false });
    }

    private static AnalysisResult RecalculateSnapshot(AnalysisResult snapshot)
    {
        var scoringService = new ScoringService();
        var categoryScores = scoringService.CalculateCategoryScores(snapshot.Issues);
        var overallScore = scoringService.CalculateOverallScore(categoryScores, snapshot.Issues);
        var architectureMap = snapshot.ArchitectureMap ?? new ArchitectureMap
        {
            Layers = [],
            Projects = [],
            Dependencies = [],
            Violations = []
        };
        var engineeringAssessment = new EngineeringAssessmentService().Build(
            overallScore,
            categoryScores,
            snapshot.Issues,
            architectureMap);

        return snapshot with
        {
            OverallScore = overallScore,
            CategoryScores = categoryScores,
            EngineeringAssessment = engineeringAssessment
        };
    }
}
