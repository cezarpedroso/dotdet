namespace Forge.Api.Models;

public static class AnalysisSourceTypes
{
    public const string GitHubRepo = "GitHubRepo";
    public const string ZipUpload = "ZipUpload";
    public const string SampleProject = "SampleProject";
    public const string LocalDevPath = "LocalDevPath";
}

public sealed record AnalysisHistoryRun
{
    public required string Id { get; init; }

    public string? UserId { get; init; }

    public required string SolutionName { get; init; }

    public required string SourceType { get; init; }

    public required string SourceLabel { get; init; }

    public string? SourceUrl { get; init; }

    public string? GitHubOwner { get; init; }

    public string? GitHubRepo { get; init; }

    public string? GitRef { get; init; }

    public string? RepositoryVisibility { get; init; }

    public required int Score { get; init; }

    public required string Grade { get; init; }

    public required string Status { get; init; }

    public required int OpenFindingCount { get; init; }

    public required int TotalFindingCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public required AnalysisResult ReportSnapshot { get; init; }
}

public sealed record AnalysisHistorySummary
{
    public required string Id { get; init; }

    public required string SolutionName { get; init; }

    public required string SourceType { get; init; }

    public required string SourceLabel { get; init; }

    public string? SourceUrl { get; init; }

    public string? GitHubOwner { get; init; }

    public string? GitHubRepo { get; init; }

    public string? GitRef { get; init; }

    public string? RepositoryVisibility { get; init; }

    public required int Score { get; init; }

    public required string Grade { get; init; }

    public required string Status { get; init; }

    public required int OpenFindingCount { get; init; }

    public required int TotalFindingCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public required bool CanRerun { get; init; }
}

public sealed record AnalysisHistoryDetail
{
    public required AnalysisHistorySummary Summary { get; init; }

    public required AnalysisResult Result { get; init; }
}

public sealed record GitHubRepositoryListingResponse
{
    public bool IsAvailable { get; init; }

    public string? Reason { get; init; }

    public required bool Enabled { get; init; }

    public required string Message { get; init; }

    public bool PrivateAccessEnabled { get; init; }

    public string? PrivateAccessMessage { get; init; }

    public IReadOnlyList<GitHubRepositorySummary> Repositories { get; init; } = Array.Empty<GitHubRepositorySummary>();
}

public sealed record GitHubRepositorySummary
{
    public required string Owner { get; init; }

    public required string Name { get; init; }

    public string? Visibility { get; init; }

    public string? DefaultBranch { get; init; }

    public string? Description { get; init; }

    public string? HtmlUrl { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed record AnalyzePublicRepositoryRequest
{
    public string? Repository { get; init; }

    public string? RepositoryUrl { get; init; }
}
