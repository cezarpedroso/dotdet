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
        var activeIssues = issues
            .Where(issue => issue.Suppression is not { IsExpired: false })
            .ToArray();
        var severityCounts = activeIssues
            .GroupBy(issue => issue.Severity)
            .ToDictionary(group => group.Key, group => group.Count());
        var blockerCount = GetCount(severityCounts, IssueSeverity.Critical) + GetCount(severityCounts, IssueSeverity.Error);
        var status = GetReadinessStatus(overallScore, blockerCount, GetCount(severityCounts, IssueSeverity.Warning));

        return new EngineeringAssessmentSummary
        {
            OverallProductionReadiness = BuildOverallReadiness(overallScore, status, blockerCount, activeIssues),
            ScoreExplanation = BuildScoreExplanation(overallScore, categoryScores, activeIssues),
            StrongAreas = BuildStrongAreas(categoryScores, activeIssues),
            HighestRisks = BuildHighestRisks(categoryScores, activeIssues),
            ArchitecturalObservations = BuildArchitectureObservations(activeIssues, architectureMap),
            SecurityObservations = BuildCategoryObservations(activeIssues, AnalysisCategories.Security, "Security and configuration"),
            ApiReadinessObservations = BuildCategoryObservations(activeIssues, AnalysisCategories.ApiReadiness, "API readiness"),
            MaintainabilityObservations = BuildMaintainabilityObservations(activeIssues, architectureMap),
            RecommendedPriorities = BuildRecommendedPriorities(activeIssues, architectureMap)
        };
    }

    private static string BuildScoreExplanation(
        int overallScore,
        CategoryScores scores,
        IReadOnlyList<AnalysisIssue> issues)
    {
        var activeIssues = issues
            .Where(issue => issue.Suppression is not { IsExpired: false })
            .ToArray();
        var rootCauseCount = activeIssues
            .Select(issue => issue.RootCauseKey ?? $"{issue.RuleId ?? issue.Id}|{issue.ProjectName}|{issue.FilePath}|{issue.Title}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var topRootCauses = GetOrderedIssues(activeIssues, null)
            .DistinctBy(issue => issue.RootCauseKey ?? $"{issue.RuleId ?? issue.Id}|{issue.ProjectName}|{issue.FilePath}|{issue.Title}", StringComparer.OrdinalIgnoreCase)
            .GroupBy(GetScoreExplanationBucket, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(BuildScoreRootCauseSummary)
            .ToArray();

        var rootCauseText = topRootCauses.Length > 0
            ? $" Major root causes include {string.Join("; ", topRootCauses)}."
            : " No active root-cause findings were detected.";

        return $"DotDet calculated the {overallScore}/100 readiness score from weighted category scores (Security {scores.Security}, API {scores.ApiReadiness}, EF Core {scores.EfCore}, Dependency Injection {scores.DependencyInjection}, Architecture {scores.Architecture}), finding severity, confidence, suppression state, and release-impact caps across {rootCauseCount} active root-cause finding(s).{rootCauseText}";
    }

    private static string GetScoreExplanationBucket(AnalysisIssue issue)
    {
        var ruleId = issue.RuleId ?? issue.Id;

        return ruleId switch
        {
            "DI001" => "DI001",
            "DI002" => "DI002",
            "EF004" => "EF004",
            "EF005" => "EF005",
            _ => issue.RootCauseKey ?? $"{ruleId}|{issue.ProjectName}|{issue.FilePath}|{issue.Title}"
        };
    }

    private static string BuildScoreRootCauseSummary(IGrouping<string, AnalysisIssue> group)
    {
        var representative = group.First();
        var ruleId = representative.RuleId ?? representative.Id;

        return ruleId switch
        {
            "DI001" when group.Count() > 1 => "duplicate DI registrations",
            "DI001" => CleanRootCauseTitle(representative.Title),
            "DI002" when group.Count() > 1 => "unregistered constructor dependencies",
            "DI002" => CleanRootCauseTitle(representative.Title),
            "SEC004" => "SEC004 authentication middleware mismatch",
            _ => $"{ruleId} {CleanRootCauseTitle(representative.Title)}"
        };
    }

    private static string CleanRootCauseTitle(string title)
    {
        return title
            .Replace("Duplicate dependency injection registration", "duplicate DI registration", StringComparison.OrdinalIgnoreCase)
            .Replace("Constructor dependency appears unregistered", "unregistered constructor dependency", StringComparison.OrdinalIgnoreCase);
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
            var concernText = GetConcernText(issues);
            return $"Status: {status}. Score: {overallScore}/100. The solution has {blockerCount} critical/error production findings, with the highest risk concentrated in {concernText}.";
        }

        if (blockerCount > 0)
        {
            var concernText = GetConcernText(issues);
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
        var profiles = GetCategoryProfiles(scores, issues);
        var riskProfiles = profiles
            .Where(profile => profile.BlockerCount > 0 || profile.RiskIssueCount > 0 || profile.Score < 100)
            .ToArray();

        if (riskProfiles.Any(profile => profile.BlockerCount > 0 || profile.RiskIssueCount > 0))
        {
            riskProfiles = riskProfiles
                .Where(profile => profile.BlockerCount > 0 || profile.RiskIssueCount > 0)
                .ToArray();
        }

        var highestRisks = riskProfiles
            .OrderByDescending(profile => profile.BlockerCount)
            .ThenBy(profile => profile.Score)
            .ThenByDescending(profile => profile.RiskIssueCount)
            .Take(3)
            .Select(profile =>
            {
                if (profile.BlockerCount > 0)
                {
                    return $"{profile.Label}: {profile.BlockerCount} critical/error findings and score {profile.Score}/100.";
                }

                return $"{profile.Label}: score {profile.Score}/100 with {profile.RiskIssueCount} findings to review.";
            })
            .ToArray();

        return highestRisks.Length > 0
            ? highestRisks
            : ["No high-priority category risks were detected."];
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
        var maintainabilityIssues = GetOrderedIssues(issues, null)
            .Where(issue => issue.Category is AnalysisCategories.Architecture or AnalysisCategories.DependencyInjection or AnalysisCategories.EfCore)
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
            .GroupBy(GetRoadmapGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var representative = group
                    .OrderByDescending(issue => issue.Severity)
                    .ThenBy(issue => GetPriorityRank(issue))
                    .ThenByDescending(issue => issue.Confidence ?? IssueConfidence.Medium)
                    .First();
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
                    Rule = representative.RuleId ?? representative.Id,
                    representative.Title,
                    representative.Recommendation,
                    representative.Category,
                    Count = group.Count(),
                    Titles = group
                        .Select(issue => issue.Title)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(3)
                        .ToArray(),
                    HighestSeverity = highestSeverity,
                    HighestConfidence = group.Max(issue => issue.Confidence ?? IssueConfidence.Medium),
                    Scope = scope ?? "solution scope"
                };
            })
            .OrderByDescending(item => item.HighestSeverity)
            .ThenBy(item => GetPriorityRank(item.Category, item.Rule))
            .ThenByDescending(item => item.HighestConfidence)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.Title)
            .Take(5)
            .Select(item => BuildRoadmapItem(
                item.Rule,
                item.Title,
                item.Recommendation,
                item.Count,
                item.Scope,
                item.Titles))
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

    private static string GetRoadmapGroupKey(AnalysisIssue issue)
    {
        var ruleId = issue.RuleId ?? issue.Id;

        return ruleId switch
        {
            "DI001" or "DI002" => $"{ruleId}|{issue.ProjectName ?? "Solution"}",
            _ => issue.RootCauseKey ?? $"{ruleId}|{issue.Category}|{issue.ProjectName}|{issue.Title}"
        };
    }

    private static string BuildRoadmapItem(
        string ruleId,
        string title,
        string recommendation,
        int count,
        string scope,
        IReadOnlyList<string> titles)
    {
        if (ruleId == "DI001" && count > 1)
        {
            return $"DI001: Consolidate duplicate registrations in {scope}: {string.Join(", ", titles)}.";
        }

        if (ruleId == "DI002" && count > 1)
        {
            return $"DI002: Register missing dependencies in {scope}: {string.Join(", ", titles)}.";
        }

        return count > 1
            ? $"{ruleId}: {title} in {scope}. {recommendation}"
            : $"{ruleId}: {recommendation}";
    }

    private static IReadOnlyList<string> GetTopConcernLabels(IReadOnlyList<AnalysisIssue> issues, int count)
    {
        return GetOrderedIssues(issues, null)
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

    private static string GetConcernText(IReadOnlyList<AnalysisIssue> issues)
    {
        var concerns = GetTopConcernLabels(issues, 3);

        return concerns.Count > 0
            ? string.Join(", ", concerns)
            : "the active findings";
    }

    private static IEnumerable<AnalysisIssue> GetOrderedIssues(IReadOnlyList<AnalysisIssue> issues, string? category)
    {
        var candidates = issues
            .Where(issue => category is null || issue.Category == category)
            .Where(issue => issue.Severity != IssueSeverity.Info)
            .ToArray();

        if (candidates.Any(issue => (issue.Confidence ?? IssueConfidence.Medium) != IssueConfidence.Low))
        {
            candidates = candidates
                .Where(issue => (issue.Confidence ?? IssueConfidence.Medium) != IssueConfidence.Low)
                .ToArray();
        }

        return candidates
            .OrderByDescending(issue => issue.Severity)
            .ThenByDescending(issue => issue.Confidence ?? IssueConfidence.Medium)
            .ThenBy(issue => GetPriorityRank(issue))
            .ThenBy(issue => issue.ProjectName)
            .ThenBy(issue => issue.Title);
    }

    private static int GetPriorityRank(AnalysisIssue issue)
    {
        return GetPriorityRank(issue.Category, issue.RuleId ?? issue.Id);
    }

    private static int GetPriorityRank(string category, string ruleId)
    {
        if (category == AnalysisCategories.Security && ruleId.Contains("AUTH", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (category == AnalysisCategories.Security)
        {
            return 2;
        }

        if (category == AnalysisCategories.Architecture)
        {
            return 3;
        }

        if (category == AnalysisCategories.DependencyInjection)
        {
            return 4;
        }

        if (category == AnalysisCategories.EfCore)
        {
            return 5;
        }

        if (category == AnalysisCategories.ApiReadiness)
        {
            return 6;
        }

        return 7;
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
        var riskIssues = categoryIssues
            .Where(issue =>
                issue.Severity != IssueSeverity.Info
                && (issue.Confidence ?? IssueConfidence.Medium) != IssueConfidence.Low)
            .ToArray();

        return new CategoryProfile(
            category,
            label,
            score,
            categoryIssues.Length,
            riskIssues.Length,
            riskIssues.Count(issue => issue.Severity is IssueSeverity.Critical or IssueSeverity.Error));
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
        int RiskIssueCount,
        int BlockerCount);
}
