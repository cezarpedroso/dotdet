using Forge.Api.Analysis;
using Forge.Api.Models;

namespace Forge.Api.Services;

public sealed class ScoringService
{
    public int CalculateOverallScore(IEnumerable<AnalysisIssue> issues)
    {
        var issueList = issues.ToArray();
        return CalculateOverallScore(CalculateCategoryScores(issueList), issueList);
    }

    public int CalculateOverallScore(CategoryScores categoryScores, IEnumerable<AnalysisIssue> issues)
    {
        var weightedScore =
            (categoryScores.Security * 0.25)
            + (categoryScores.ApiReadiness * 0.20)
            + (categoryScores.EfCore * 0.20)
            + (categoryScores.DependencyInjection * 0.20)
            + (categoryScores.Architecture * 0.15);

        return ApplyReleaseImpactCaps((int)Math.Round(weightedScore, MidpointRounding.AwayFromZero), issues);
    }

    public CategoryScores CalculateCategoryScores(IEnumerable<AnalysisIssue> issues)
    {
        var issueList = issues.ToArray();

        return new CategoryScores
        {
            Architecture = CalculateScoreForCategory(issueList, AnalysisCategories.Architecture),
            DependencyInjection = CalculateScoreForCategory(issueList, AnalysisCategories.DependencyInjection),
            EfCore = CalculateScoreForCategory(issueList, AnalysisCategories.EfCore),
            Security = CalculateScoreForCategory(issueList, AnalysisCategories.Security),
            ApiReadiness = CalculateScoreForCategory(issueList, AnalysisCategories.ApiReadiness)
        };
    }

    private static int CalculateScoreForCategory(IEnumerable<AnalysisIssue> issues, string category)
    {
        return CalculateScore(issues.Where(issue => issue.Category.Equals(category, StringComparison.OrdinalIgnoreCase)));
    }

    private static int CalculateScore(IEnumerable<AnalysisIssue> issues)
    {
        var groupedIssues = issues
            .Where(AnalyzerUtilities.IsActiveProductionFinding)
            .GroupBy(issue => issue.RootCauseKey
                ?? $"{issue.RuleId ?? issue.Id}|{issue.Category}|{issue.ProjectName ?? "Solution"}|{issue.FilePath ?? string.Empty}");
        var penalty = groupedIssues.Sum(group =>
        {
            var orderedPenalties = group
                .Select(GetPenalty)
                .OrderByDescending(penalty => penalty)
                .ToArray();
            if (orderedPenalties.Length == 0)
            {
                return 0;
            }

            var basePenalty = orderedPenalties[0];
            var repeatPenalty = orderedPenalties
                .Skip(1)
                .Sum(penalty => Math.Max(1, penalty / 3));

            return basePenalty + Math.Min(repeatPenalty, basePenalty);
        });

        return Math.Max(0, 100 - penalty);
    }

    private static int ApplyReleaseImpactCaps(int score, IEnumerable<AnalysisIssue> issues)
    {
        var activeIssues = issues.Where(AnalyzerUtilities.IsActiveProductionFinding).ToArray();
        var activeCriticalCount = activeIssues
            .Where(issue =>
                issue.Severity == IssueSeverity.Critical)
            .Select(GetRootCauseKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var confirmedErrorCount = activeIssues
            .Where(issue => issue.Severity == IssueSeverity.Error && issue.Confidence == IssueConfidence.High)
            .Select(GetRootCauseKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var criticalCap = activeCriticalCount switch
        {
            0 => 100,
            <= 3 => 82,
            <= 8 => 68,
            _ => 49
        };

        var confirmedErrorCap = confirmedErrorCount switch
        {
            0 => 100,
            <= 2 => 88,
            <= 5 => 85,
            _ => 82
        };

        return Math.Min(score, Math.Min(criticalCap, confirmedErrorCap));
    }

    private static int GetPenalty(AnalysisIssue issue)
    {
        var severityPenalty = GetPenalty(issue.Severity);

        return issue.Confidence switch
        {
            IssueConfidence.Low => Math.Min(severityPenalty, 1),
            IssueConfidence.Medium => severityPenalty,
            IssueConfidence.High => severityPenalty,
            _ => severityPenalty
        };
    }

    private static int GetPenalty(IssueSeverity severity)
    {
        return severity switch
        {
            IssueSeverity.Critical => 15,
            IssueSeverity.Error => 8,
            IssueSeverity.Warning => 4,
            IssueSeverity.Info => 1,
            _ => 0
        };
    }

    private static string GetRootCauseKey(AnalysisIssue issue)
    {
        return issue.RootCauseKey
            ?? $"{issue.RuleId ?? issue.Id}|{issue.Category}|{issue.ProjectName ?? "Solution"}|{issue.FilePath ?? string.Empty}";
    }
}
