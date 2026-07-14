using System.Text.Json;
using Forge.Api.Analysis;
using Forge.Api.Models;

namespace Forge.Api.Services;

public sealed class SuppressionService
{
    public const string SuppressionFileName = "dotdet.suppressions.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string GetSuppressionFilePath(string solutionPath)
    {
        var rootDirectory = Path.GetDirectoryName(SolutionAnalysisService.ResolveSolutionPath(solutionPath))
            ?? Directory.GetCurrentDirectory();
        return Path.Combine(rootDirectory, SuppressionFileName);
    }

    public DotDetSuppressionFile Load(string solutionPath)
    {
        var suppressionFilePath = GetSuppressionFilePath(solutionPath);
        if (!File.Exists(suppressionFilePath))
        {
            return new DotDetSuppressionFile();
        }

        try
        {
            var file = JsonSerializer.Deserialize<DotDetSuppressionFile>(
                File.ReadAllText(suppressionFilePath),
                JsonOptions);

            return file ?? new DotDetSuppressionFile();
        }
        catch (JsonException)
        {
            return new DotDetSuppressionFile();
        }
    }

    public IReadOnlyList<AnalysisIssue> Apply(
        IReadOnlyList<AnalysisIssue> issues,
        string solutionPath,
        DotDetSuppressionFile suppressionFile)
    {
        var rootDirectory = Path.GetDirectoryName(SolutionAnalysisService.ResolveSolutionPath(solutionPath))
            ?? Directory.GetCurrentDirectory();
        var activeSuppressions = suppressionFile.Suppressions
            .Where(suppression => !IsExpired(suppression))
            .ToArray();

        return issues
            .Select(issue =>
            {
                var suppression = activeSuppressions.FirstOrDefault(candidate => Matches(candidate, issue, rootDirectory));
                return suppression is null
                    ? issue
                    : issue with { Suppression = ToFindingSuppression(suppression) };
            })
            .ToArray();
    }

    public static RepositorySuppression CreateForIssue(
        AnalysisIssue issue,
        string? reason,
        string? status,
        DateTimeOffset? expiration)
    {
        var now = DateTimeOffset.UtcNow;
        return new RepositorySuppression
        {
            Id = $"sup_{Guid.NewGuid():N}",
            RuleId = issue.RuleId ?? issue.Id,
            File = NormalizeStoredFile(issue.FilePath),
            Project = string.IsNullOrWhiteSpace(issue.ProjectName) ? null : issue.ProjectName,
            Reason = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim(),
            Status = NormalizeStatus(status),
            CreatedDate = now,
            Expiration = expiration
        };
    }

    public static AnalysisIssue ApplyToIssue(AnalysisIssue issue, RepositorySuppression suppression)
    {
        return issue with { Suppression = ToFindingSuppression(suppression) };
    }

    private static bool Matches(RepositorySuppression suppression, AnalysisIssue issue, string rootDirectory)
    {
        if (!suppression.RuleId.Equals(issue.RuleId ?? issue.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(suppression.Project)
            && !suppression.Project.Equals(issue.ProjectName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(suppression.File))
        {
            return true;
        }

        var issueFile = NormalizeSuppressionFile(issue.FilePath, rootDirectory);
        return !string.IsNullOrWhiteSpace(issueFile)
            && issueFile.Equals(suppression.File, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameScope(RepositorySuppression left, RepositorySuppression right)
    {
        return left.RuleId.Equals(right.RuleId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Project, right.Project, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.File, right.File, StringComparison.OrdinalIgnoreCase);
    }

    private static FindingSuppression ToFindingSuppression(RepositorySuppression suppression)
    {
        return new FindingSuppression
        {
            Id = suppression.Id,
            RuleId = suppression.RuleId,
            File = suppression.File,
            Project = suppression.Project,
            Reason = suppression.Reason,
            Status = suppression.Status,
            CreatedDate = suppression.CreatedDate,
            Expiration = suppression.Expiration,
            IsExpired = IsExpired(suppression)
        };
    }

    public static string NormalizeStatus(string? status)
    {
        return status?.Trim() switch
        {
            "FalsePositive" => "False Positive",
            "False Positive" => "False Positive",
            "AcceptedRisk" => "Accepted Risk",
            "Accepted Risk" => "Accepted Risk",
            "Ignore" => "Ignore",
            _ => "Ignore"
        };
    }

    private static string? NormalizeSuppressionFile(string? filePath, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var normalizedRoot = AnalyzerUtilities.NormalizePath(rootDirectory);
        var normalizedFile = Path.IsPathRooted(filePath)
            ? AnalyzerUtilities.NormalizePath(filePath)
            : filePath.Trim();

        if (Path.IsPathRooted(normalizedFile)
            && normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            normalizedFile = Path.GetRelativePath(normalizedRoot, normalizedFile);
        }

        return normalizedFile.Replace('\\', '/');
    }

    private static string? NormalizeStoredFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var normalizedFile = filePath.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(normalizedFile) || normalizedFile.StartsWith("//", StringComparison.Ordinal))
        {
            return Path.GetFileName(normalizedFile);
        }

        return normalizedFile;
    }

    private static bool IsExpired(RepositorySuppression suppression)
    {
        return suppression.Expiration is not null && suppression.Expiration < DateTimeOffset.UtcNow;
    }
}
