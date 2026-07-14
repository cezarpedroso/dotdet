namespace Forge.Api.Contracts;

public sealed record CreateSuppressionRequest
{
    public string? AnalysisRunId { get; init; }

    public string? FindingId { get; init; }

    public string? RuleId { get; init; }

    public string? File { get; init; }

    public string? Project { get; init; }

    public string? Reason { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? Expiration { get; init; }

    public string? SolutionPath { get; init; }
}
