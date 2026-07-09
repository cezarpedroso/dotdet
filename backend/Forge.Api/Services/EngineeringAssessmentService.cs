using Forge.Api.Analysis;
using Forge.Api.Models;

namespace Forge.Api.Services;

public sealed class EngineeringAssessmentService
{
    public EngineeringAssessmentSummary Build(
        int overallScore,
        CategoryScores categoryScores,
        IReadOnlyList<AnalysisIssue> issues,
        ArchitectureMap architectureMap)
    {
        var severityCounts = issues
            .GroupBy(issue => issue.Severity)
            .ToDictionary(group => group.Key, group => group.Count());
        var blockerCount = GetCount(severityCounts, IssueSeverity.Critical) + GetCount(severityCounts, IssueSeverity.Error);
        var status = GetReadinessStatus(overallScore, blockerCount, GetCount(severityCounts, IssueSeverity.Warning));

        return new EngineeringAssessmentSummary
        {
            OverallProductionReadiness = BuildOverallReadiness(overallScore, status, blockerCount, issues),
            StrongAreas = BuildStrongAreas(categoryScores, issues),
            HighestRisks = BuildHighestRisks(categoryScores, issues),
            ArchitecturalObservations = BuildArchitectureObservations(issues, architectureMap),
            SecurityObservations = BuildCategoryObservations(issues, AnalysisCategories.Security, "Security and configuration"),
            ApiReadinessObservations = BuildCategoryObservations(issues, AnalysisCategories.ApiReadiness, "API readiness"),
            MaintainabilityObservations = BuildMaintainabilityObservations(issues, architectureMap),
            RecommendedPriorities = BuildRecommendedPriorities(issues, architectureMap)
        };
    }

    private static string BuildOverallReadiness(
        int overallScore,
        string status,
        int blockerCount,
        IReadOnlyList<AnalysisIssue> issues)
    {
        var criticalCount = issues.Count(issue => issue.Severity == IssueSeverity.Critical);

        if (criticalCount > 0 || (blockerCount >= 4 && overallScore < 60))
        {
            var concernText = string.Join(", ", GetTopConcernLabels(issues, 3));
            return $"Status: {status}. Score: {overallScore}/100. The solution has {blockerCount} critical/error production findings, with the highest risk concentrated in {concernText}.";
        }

        if (blockerCount > 0)
        {
            var concernText = string.Join(", ", GetTopConcernLabels(issues, 3));
            return $"Status: {status}. Score: {overallScore}/100. Review {blockerCount} high-severity findings before production hardening, especially around {concernText}.";
        }

        var warnings = issues.Count(issue => issue.Severity == IssueSeverity.Warning);
        if (warnings > 0)
        {
            return $"Status: {status}. Score: {overallScore}/100. No release-blocking findings were detected, but {warnings} warning-level items should be reviewed before production hardening.";
        }

        return $"Status: {status}. Score: {overallScore}/100. DotDet did not detect major production-readiness blockers in the analyzed architecture, configuration, persistence, dependency injection, or API surface.";
    }

    private static IReadOnlyList<string> BuildStrongAreas(CategoryScores scores, IReadOnlyList<AnalysisIssue> issues)
    {
        var areas = GetCategoryProfiles(scores, issues)
            .Where(profile => profile.Score >= 85 && profile.BlockerCount == 0)
            .OrderByDescending(profile => profile.Score)
            .ThenBy(profile => profile.IssueCount)
            .Select(profile => $"{profile.Label}: {profile.Score}/100 with no critical/error findings.")
            .ToArray();

        return areas.Length > 0
            ? areas
            : ["No category is clean enough to call out as a strong area yet."];
    }

    private static IReadOnlyList<string> BuildHighestRisks(CategoryScores scores, IReadOnlyList<AnalysisIssue> issues)
    {
        return GetCategoryProfiles(scores, issues)
            .OrderByDescending(profile => profile.BlockerCount)
            .ThenBy(profile => profile.Score)
            .ThenByDescending(profile => profile.IssueCount)
            .Take(3)
            .Select(profile =>
            {
                if (profile.BlockerCount > 0)
                {
                    return $"{profile.Label}: {profile.BlockerCount} critical/error findings and score {profile.Score}/100.";
                }

                return $"{profile.Label}: score {profile.Score}/100 with {profile.IssueCount} findings to review.";
            })
            .ToArray();
    }

    private static IReadOnlyList<string> BuildArchitectureObservations(
        IReadOnlyList<AnalysisIssue> issues,
        ArchitectureMap map)
    {
        var observations = new List<string>();

        if (map.Projects.Count > 0)
        {
            observations.Add($"DotDet inferred {map.Layers.Count} logical layer(s) across {map.Projects.Count} project(s) and {map.Dependencies.Count} project reference(s).");
        }

        if (map.Violations.Count > 0)
        {
            observations.Add($"{map.Violations.Count} architecture map violation(s) were detected, including {map.Violations[0].Description}");
        }

        observations.AddRange(GetTopIssueSummaries(issues, AnalysisCategories.Architecture, 2));

        return observations.Count > 0
            ? observations
            : ["No architecture boundary findings were detected from the loaded project graph."];
    }

    private static IReadOnlyList<string> BuildCategoryObservations(
        IReadOnlyList<AnalysisIssue> issues,
        string category,
        string label)
    {
        var categoryIssues = GetOrderedIssues(issues, category).Take(3).ToArray();

        if (categoryIssues.Length == 0)
        {
            return [$"{label}: no major findings were detected."];
        }

        return categoryIssues
            .Select(issue => $"{label}: {issue.Title} ({issue.Severity}) in {issue.ProjectName ?? "solution scope"}.")
            .ToArray();
    }

    private static IReadOnlyList<string> BuildMaintainabilityObservations(
        IReadOnlyList<AnalysisIssue> issues,
        ArchitectureMap map)
    {
        var maintainabilityIssues = issues
            .Where(issue => issue.Category is AnalysisCategories.Architecture or AnalysisCategories.DependencyInjection or AnalysisCategories.EfCore)
            .OrderByDescending(issue => issue.Severity)
            .Take(3)
            .Select(issue => $"{issue.Title} can increase operational or change risk if left unresolved.")
            .ToList();

        if (map.Dependencies.Count > 0)
        {
            var invalidCount = map.Dependencies.Count(dependency => dependency.IsViolation);
            maintainabilityIssues.Insert(0, $"{invalidCount} of {map.Dependencies.Count} project dependencies appear to violate or stress the inferred architecture boundaries.");
        }

        return maintainabilityIssues.Count > 0
            ? maintainabilityIssues
            : ["Maintainability risk is low based on the current analyzer findings."];
    }

    private static IReadOnlyList<string> BuildRecommendedPriorities(
        IReadOnlyList<AnalysisIssue> issues,
        ArchitectureMap map)
    {
        var priorities = GetOrderedIssues(issues, null)
            .Where(issue => issue.Severity is IssueSeverity.Critical or IssueSeverity.Error or IssueSeverity.Warning)
            .GroupBy(issue => new { Rule = issue.RuleId ?? issue.Id, issue.Title, issue.Recommendation })
            .Select(group =>
            {
                var highestSeverity = group.Max(issue => issue.Severity);
                var productionProjects = group
                    .Select(issue => issue.ProjectName)
                    .Where(project => !string.IsNullOrWhiteSpace(project))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var scope = productionProjects.Length switch
                {
                    0 => "solution scope",
                    1 => productionProjects[0],
                    _ => $"{productionProjects.Length} production projects"
                };

                return new
                {
                    group.Key.Rule,
                    group.Key.Title,
                    group.Key.Recommendation,
                    Count = group.Count(),
                    HighestSeverity = highestSeverity,
                    Scope = scope
                };
            })
            .OrderByDescending(item => item.HighestSeverity)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.Title)
            .Take(5)
            .Select(item => item.Count > 1
                ? $"{item.Rule}: {item.Title} in {item.Scope}. {item.Recommendation}"
                : $"{item.Rule}: {item.Recommendation}")
            .ToList();

        if (map.Violations.Any(violation => violation.RelatedFindingId is null))
        {
            priorities.Add("Review architecture map-only dependency risks and decide whether to refactor or document accepted exceptions.");
        }

        return priorities.Count > 0
            ? priorities
            : ["No immediate remediation priorities were identified."];
    }

    private static IEnumerable<string> GetTopIssueSummaries(
        IReadOnlyList<AnalysisIssue> issues,
        string category,
        int count)
    {
        return GetOrderedIssues(issues, category)
            .Take(count)
            .Select(issue => $"{issue.Title} ({issue.Severity}) affects {issue.ProjectName ?? "solution scope"}.");
    }

    private static IReadOnlyList<string> GetTopConcernLabels(IReadOnlyList<AnalysisIssue> issues, int count)
    {
        return issues
            .GroupBy(issue => issue.Category)
            .Select(group => new
            {
                Category = group.Key,
                Weight = group.Sum(issue => issue.Severity switch
                {
                    IssueSeverity.Critical => 20,
                    IssueSeverity.Error => 12,
                    IssueSeverity.Warning => 5,
                    _ => 1
                })
            })
            .OrderByDescending(item => item.Weight)
            .Take(count)
            .Select(item => GetCategoryLabel(item.Category))
            .ToArray();
    }

    private static IEnumerable<AnalysisIssue> GetOrderedIssues(IReadOnlyList<AnalysisIssue> issues, string? category)
    {
        return issues
            .Where(issue => category is null || issue.Category == category)
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.ProjectName)
            .ThenBy(issue => issue.Title);
    }

    private static IReadOnlyList<CategoryProfile> GetCategoryProfiles(CategoryScores scores, IReadOnlyList<AnalysisIssue> issues)
    {
        return
        [
            CreateProfile(AnalysisCategories.Architecture, "Architecture boundaries", scores.Architecture, issues),
            CreateProfile(AnalysisCategories.Security, "Security and configuration", scores.Security, issues),
            CreateProfile(AnalysisCategories.EfCore, "EF Core and migrations", scores.EfCore, issues),
            CreateProfile(AnalysisCategories.DependencyInjection, "Dependency injection reliability", scores.DependencyInjection, issues),
            CreateProfile(AnalysisCategories.ApiReadiness, "API reliability", scores.ApiReadiness, issues)
        ];
    }

    private static CategoryProfile CreateProfile(
        string category,
        string label,
        int score,
        IReadOnlyList<AnalysisIssue> issues)
    {
        var categoryIssues = issues.Where(issue => issue.Category == category).ToArray();

        return new CategoryProfile(
            category,
            label,
            score,
            categoryIssues.Length,
            categoryIssues.Count(issue => issue.Severity is IssueSeverity.Critical or IssueSeverity.Error));
    }

    private static string GetReadinessStatus(int score, int blockerCount, int warningCount)
    {
        if (score < 50)
        {
            return "Not Ready";
        }

        return warningCount > 0 || blockerCount > 0 || score < 85 ? "Needs Review" : "Ready";
    }

    private static string GetCategoryLabel(string category)
    {
        return category switch
        {
            AnalysisCategories.Architecture => "architecture boundaries",
            AnalysisCategories.DependencyInjection => "dependency injection",
            AnalysisCategories.EfCore => "EF Core and migrations",
            AnalysisCategories.Security => "security and configuration",
            AnalysisCategories.ApiReadiness => "API readiness",
            _ => category
        };
    }

    private static int GetCount(IReadOnlyDictionary<IssueSeverity, int> counts, IssueSeverity severity)
    {
        return counts.GetValueOrDefault(severity, 0);
    }

    private sealed record CategoryProfile(
        string Category,
        string Label,
        int Score,
        int IssueCount,
        int BlockerCount);
}
