using Forge.Api.Models;

namespace Forge.Api.Services;

public sealed class FindingGroupingService
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    public IReadOnlyList<AnalysisIssue> Group(IEnumerable<AnalysisIssue> issues, string? repositoryRoot = null)
    {
        var issueList = issues.ToArray();
        var groupedIssueIds = new HashSet<string>(StringComparer.Ordinal);
        var grouped = new List<AnalysisIssue>();

        grouped.AddRange(GroupEfMigrationIssues(issueList, groupedIssueIds, repositoryRoot));
        grouped.AddRange(GroupDuplicateRegistrations(issueList, groupedIssueIds));
        grouped.AddRange(GroupUnregisteredDependencies(issueList, groupedIssueIds));
        grouped.AddRange(GroupConnectionStrings(issueList, groupedIssueIds, repositoryRoot));

        grouped.AddRange(issueList.Where(issue => !groupedIssueIds.Contains(issue.Id)));

        return grouped
            .Select((issue, index) => issue with
            {
                Id = $"{issue.RuleId ?? issue.Id}-{index + 1:D3}",
                RootCauseKey = SanitizeRootCauseKey(
                    issue.RootCauseKey ?? BuildDefaultRootCauseKey(issue, repositoryRoot),
                    repositoryRoot)
            })
            .ToArray();
    }

    private static IEnumerable<AnalysisIssue> GroupEfMigrationIssues(
        IReadOnlyList<AnalysisIssue> issues,
        ISet<string> groupedIssueIds,
        string? repositoryRoot)
    {
        foreach (var ruleId in new[] { "EF004", "EF005" })
        {
            foreach (var group in issues
                .Where(issue => issue.RuleId == ruleId && !string.IsNullOrWhiteSpace(issue.FilePath))
                .GroupBy(issue => new
                {
                    issue.RuleId,
                    issue.ProjectName,
                    FilePath = NormalizePath(issue.FilePath!)
                }))
            {
                if (group.Count() <= 1)
                {
                    continue;
                }

                foreach (var issue in group)
                {
                    groupedIssueIds.Add(issue.Id);
                }

                var issueGroup = group
                    .OrderBy(issue => issue.LineNumber ?? int.MaxValue)
                    .ToArray();
                var first = issueGroup[0];
                var operationLabel = ruleId == "EF004"
                    ? "destructive schema operations"
                    : "raw SQL operations";
                var evidence = issueGroup
                    .Select(issue => new AnalysisEvidence
                    {
                        Label = ExtractOperation(issue),
                        Detail = $"{ExtractOperation(issue)} at line {issue.LineNumber?.ToString() ?? "unknown"}",
                        FilePath = issue.FilePath,
                        LineNumber = issue.LineNumber
                    })
                    .ToArray();

                yield return first with
                {
                    Title = ruleId == "EF004"
                        ? "Migration contains destructive schema operations"
                        : "Migration executes raw SQL operations",
                    Description = $"{Path.GetFileName(first.FilePath)} contains {issueGroup.Length} {operationLabel}: {string.Join(", ", evidence.Select(item => item.Detail))}.",
                    LineNumber = issueGroup.Min(issue => issue.LineNumber),
                    Severity = issueGroup.Max(issue => issue.Severity),
                    Evidence = evidence,
                    RootCauseKey = $"{ruleId}|{first.ProjectName}|{BuildRootCausePath(first.FilePath, repositoryRoot)}",
                    WhyDetected = BuildEvidenceText(first, evidence, "Migration file contains repeated EF Core migration operations that share one deployment risk.")
                };
            }
        }
    }

    private static IEnumerable<AnalysisIssue> GroupDuplicateRegistrations(
        IReadOnlyList<AnalysisIssue> issues,
        ISet<string> groupedIssueIds)
    {
        foreach (var group in issues
            .Where(issue => issue.RuleId == "DI001")
            .GroupBy(issue => BuildGroupKey(
                issue.ProjectName ?? "Solution",
                ExtractServiceType(issue)), KeyComparer))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            foreach (var issue in group)
            {
                groupedIssueIds.Add(issue.Id);
            }

            var issueGroup = group
                .OrderBy(issue => issue.FilePath)
                .ThenBy(issue => issue.LineNumber ?? int.MaxValue)
                .ToArray();
            var first = issueGroup[0];
            var serviceType = ExtractServiceType(first);
            var evidence = issueGroup
                .SelectMany(issue => issue.Evidence is { Count: > 0 }
                    ? issue.Evidence
                    : [new AnalysisEvidence
                    {
                        Label = "Registered",
                        Detail = $"{Path.GetFileName(issue.FilePath ?? "unknown")}:{issue.LineNumber?.ToString() ?? "unknown"} - {issue.Description}",
                        FilePath = issue.FilePath,
                        LineNumber = issue.LineNumber
                    }])
                .DistinctBy(item => $"{item.Label}|{item.Detail}|{NormalizePath(item.FilePath ?? string.Empty)}|{item.LineNumber}", StringComparer.OrdinalIgnoreCase)
                .ToArray();

            yield return first with
            {
                Title = $"Duplicate registrations for {serviceType}",
                Description = $"{serviceType} appears to be registered {evidence.Length} times in {first.ProjectName ?? "the solution"}.",
                FilePath = evidence.OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.LineNumber ?? int.MaxValue).FirstOrDefault()?.FilePath ?? first.FilePath,
                LineNumber = evidence.Min(item => item.LineNumber),
                Evidence = evidence,
                RootCauseKey = $"DI001|{first.ProjectName}|{serviceType}",
                Confidence = issueGroup.Max(issue => issue.Confidence ?? IssueConfidence.Medium),
                WhyDetected = BuildEvidenceText(first, evidence, "DotDet found multiple dependency-injection registrations for the same service root cause.")
            };
        }
    }

    private static IEnumerable<AnalysisIssue> GroupUnregisteredDependencies(
        IReadOnlyList<AnalysisIssue> issues,
        ISet<string> groupedIssueIds)
    {
        foreach (var group in issues
            .Where(issue => issue.RuleId == "DI002")
            .GroupBy(issue => BuildGroupKey(
                issue.ProjectName ?? "Solution",
                ExtractInjectedDependencyType(issue)), KeyComparer))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            foreach (var issue in group)
            {
                groupedIssueIds.Add(issue.Id);
            }

            var issueGroup = group
                .OrderBy(issue => issue.FilePath)
                .ThenBy(issue => issue.LineNumber ?? int.MaxValue)
                .ToArray();
            var first = issueGroup[0];
            var dependencyType = ExtractInjectedDependencyType(first);
            var evidence = issueGroup
                .SelectMany(issue => issue.Evidence is { Count: > 0 }
                    ? issue.Evidence
                    : [new AnalysisEvidence
                    {
                        Label = "Injected by",
                        Detail = $"{ExtractOwnerType(issue)} at {Path.GetFileName(issue.FilePath ?? "unknown")}:{issue.LineNumber?.ToString() ?? "unknown"}",
                        FilePath = issue.FilePath,
                        LineNumber = issue.LineNumber
                    }])
                .DistinctBy(item => $"{item.Label}|{item.Detail}|{NormalizePath(item.FilePath ?? string.Empty)}|{item.LineNumber}", StringComparer.OrdinalIgnoreCase)
                .ToArray();

            yield return first with
            {
                Title = $"{dependencyType} appears unregistered",
                Description = $"{dependencyType} appears unregistered and is injected in {evidence.Length} constructor location(s).",
                FilePath = evidence.OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.LineNumber ?? int.MaxValue).FirstOrDefault()?.FilePath ?? first.FilePath,
                LineNumber = evidence.Min(item => item.LineNumber),
                Evidence = evidence,
                RootCauseKey = $"DI002|{first.ProjectName}|{dependencyType}",
                Confidence = issueGroup.Max(issue => issue.Confidence ?? IssueConfidence.Medium),
                WhyDetected = BuildEvidenceText(first, evidence, "DotDet grouped constructor injections that point to the same missing dependency type.")
            };
        }
    }

    private static IEnumerable<AnalysisIssue> GroupConnectionStrings(
        IReadOnlyList<AnalysisIssue> issues,
        ISet<string> groupedIssueIds,
        string? repositoryRoot)
    {
        foreach (var group in issues
            .Where(issue => issue.RuleId == "SECCONN" && !string.IsNullOrWhiteSpace(issue.FilePath))
            .GroupBy(issue => new
            {
                issue.ProjectName,
                FilePath = NormalizePath(issue.FilePath!)
            }))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            foreach (var issue in group)
            {
                groupedIssueIds.Add(issue.Id);
            }

            var issueGroup = group
                .OrderBy(issue => issue.LineNumber ?? int.MaxValue)
                .ToArray();
            var first = issueGroup[0];
            var evidence = issueGroup
                .Select(issue => new AnalysisEvidence
                {
                    Label = ExtractConfigurationKey(issue),
                    Detail = $"{ExtractConfigurationKey(issue)} at line {issue.LineNumber?.ToString() ?? "unknown"}",
                    FilePath = issue.FilePath,
                    LineNumber = issue.LineNumber
                })
                .ToArray();

            yield return first with
            {
                Title = "Connection strings found in committed configuration",
                Description = $"{Path.GetFileName(first.FilePath)} contains {issueGroup.Length} connection string value(s): {string.Join(", ", evidence.Select(item => item.Label))}.",
                Severity = issueGroup.Max(issue => issue.Severity),
                Confidence = issueGroup.Max(issue => issue.Confidence ?? IssueConfidence.Medium),
                Evidence = evidence,
                RootCauseKey = $"SECCONN|{first.ProjectName}|{BuildRootCausePath(first.FilePath, repositoryRoot)}",
                WhyDetected = BuildEvidenceText(first, evidence, "DotDet grouped committed connection string keys found in the same configuration file.")
            };
        }
    }

    private static string BuildEvidenceText(AnalysisIssue issue, IReadOnlyList<AnalysisEvidence> evidence, string detected)
    {
        var evidenceLines = evidence.Select(item =>
            $"- {item.Detail}{(string.IsNullOrWhiteSpace(item.FilePath) ? string.Empty : $" ({item.FilePath})")}");

        return string.Join(Environment.NewLine, new[]
        {
            $"Rule: {issue.RuleId ?? issue.Id}",
            $"Project: {issue.ProjectName ?? "Solution"}",
            $"File: {issue.FilePath ?? "Not available"}",
            $"Detected: {detected}",
            "Evidence:"
        }.Concat(evidenceLines));
    }

    private static string ExtractOperation(AnalysisIssue issue)
    {
        var marker = "migrationBuilder.";
        var markerIndex = issue.Description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            var start = markerIndex + marker.Length;
            var end = issue.Description.IndexOfAny(['(', '.', ' '], start);
            if (end > start)
            {
                return issue.Description[start..end];
            }
        }

        if (issue.Description.Contains("raw SQL", StringComparison.OrdinalIgnoreCase)
            || issue.Title.Contains("raw SQL", StringComparison.OrdinalIgnoreCase))
        {
            return "Sql";
        }

        return issue.RuleId == "EF004" ? "Destructive operation" : "Migration operation";
    }

    private static string ExtractServiceType(AnalysisIssue issue)
    {
        var description = issue.Description;
        var marker = " is registered ";
        var markerIndex = description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex > 0)
        {
            return description[..markerIndex].Trim();
        }

        var value = issue.Title.Replace("Duplicate registrations for ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(value) ? "service" : value.Trim();
    }

    private static string ExtractInjectedDependencyType(AnalysisIssue issue)
    {
        var marker = " injects ";
        var markerIndex = issue.Description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            var start = markerIndex + marker.Length;
            var end = issue.Description.IndexOf(',', start);
            if (end > start)
            {
                return issue.Description[start..end].Trim();
            }
        }

        var value = issue.Title.Replace(" appears unregistered", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Constructor dependency appears unregistered", "dependency", StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(value) ? "dependency" : value.Trim();
    }

    private static string ExtractOwnerType(AnalysisIssue issue)
    {
        var marker = " injects ";
        var markerIndex = issue.Description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return markerIndex > 0 ? issue.Description[..markerIndex].Trim() : issue.ProjectName ?? "Consumer";
    }

    private static string ExtractConfigurationKey(AnalysisIssue issue)
    {
        var marker = " value for ";
        var markerIndex = issue.Description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            return issue.Description[(markerIndex + marker.Length)..].TrimEnd('.').Trim();
        }

        return issue.Title;
    }

    private static string BuildDefaultRootCauseKey(AnalysisIssue issue, string? repositoryRoot)
    {
        return string.Join('|',
            issue.RuleId ?? issue.Id,
            issue.ProjectName ?? "Solution",
            BuildRootCausePath(issue.FilePath, repositoryRoot),
            issue.Title);
    }

    private static string SanitizeRootCauseKey(string rootCauseKey, string? repositoryRoot)
    {
        return string.Join('|', rootCauseKey
            .Split('|')
            .Select(segment => LooksLikePath(segment)
                ? BuildRootCausePath(segment, repositoryRoot)
                : segment));
    }

    private static string BuildRootCausePath(string? path, string? repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "<unknown-file>";
        }

        var normalizedPath = NormalizePath(path.Trim());
        if (!Path.IsPathRooted(path))
        {
            return normalizedPath.Split('/').Any(segment => segment == "..")
                ? "<unknown-file>"
                : normalizedPath.TrimStart('.', '/');
        }

        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return "<unknown-file>";
        }

        var relativePath = Path.GetRelativePath(repositoryRoot, path);
        return Path.IsPathRooted(relativePath)
            || relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment == "..")
                ? "<unknown-file>"
                : NormalizePath(relativePath);
    }

    private static bool LooksLikePath(string value)
    {
        var trimmed = value.Trim();
        return Path.IsPathRooted(trimmed)
            || trimmed.Contains('/')
            || trimmed.Contains('\\');
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string BuildGroupKey(params string?[] parts)
    {
        return string.Join('|', parts.Select(part => part?.Trim() ?? string.Empty));
    }
}
