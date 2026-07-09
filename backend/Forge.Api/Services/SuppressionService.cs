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

    public RepositorySuppression Create(CreateSuppressionInput input)
    {
        var suppressionFilePath = GetSuppressionFilePath(input.SolutionPath);
        var suppressionFile = Load(input.SolutionPath);
        var rootDirectory = Path.GetDirectoryName(SolutionAnalysisService.ResolveSolutionPath(input.SolutionPath))
            ?? Directory.GetCurrentDirectory();
        var normalizedFile = NormalizeSuppressionFile(input.File, rootDirectory);
        var now = DateTimeOffset.UtcNow;
        var suppression = new RepositorySuppression
        {
            Id = $"sup_{Guid.NewGuid():N}",
            RuleId = input.RuleId,
            File = normalizedFile,
            Project = string.IsNullOrWhiteSpace(input.Project) ? null : input.Project,
            Reason = string.IsNullOrWhiteSpace(input.Reason) ? "No reason provided." : input.Reason.Trim(),
            Status = NormalizeStatus(input.Status),
            CreatedDate = now,
            Expiration = input.Expiration
        };

        var suppressions = suppressionFile.Suppressions
            .Where(existing => !SameScope(existing, suppression))
            .Append(suppression)
            .OrderBy(existing => existing.RuleId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(existing => existing.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(existing => existing.File, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Save(suppressionFilePath, suppressionFile with { Suppressions = suppressions });
        return suppression;
    }

    public bool Remove(string solutionPath, string suppressionId)
    {
        var suppressionFilePath = GetSuppressionFilePath(solutionPath);
        var suppressionFile = Load(solutionPath);
        var suppressions = suppressionFile.Suppressions
            .Where(suppression => !suppression.Id.Equals(suppressionId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (suppressions.Length == suppressionFile.Suppressions.Count)
        {
            return false;
        }

        Save(suppressionFilePath, suppressionFile with { Suppressions = suppressions });
        return true;
    }

    private static void Save(string suppressionFilePath, DotDetSuppressionFile suppressionFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(suppressionFilePath) ?? Directory.GetCurrentDirectory());
        File.WriteAllText(suppressionFilePath, JsonSerializer.Serialize(suppressionFile, JsonOptions));
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

    private static string NormalizeStatus(string status)
    {
        return status.Trim() switch
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

    private static bool IsExpired(RepositorySuppression suppression)
    {
        return suppression.Expiration is not null && suppression.Expiration < DateTimeOffset.UtcNow;
    }
}

public sealed record CreateSuppressionInput(
    string SolutionPath,
    string RuleId,
    string? File,
    string? Project,
    string Reason,
    string Status,
    DateTimeOffset? Expiration);
