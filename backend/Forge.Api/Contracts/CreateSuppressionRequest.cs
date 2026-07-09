namespace Forge.Api.Contracts;

public sealed record CreateSuppressionRequest(
    string SolutionPath,
    string RuleId,
    string? File,
    string? Project,
    string Reason,
    string Status,
    DateTimeOffset? Expiration);
