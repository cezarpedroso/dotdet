namespace Forge.Api.Models;

public sealed record BundledSampleSummary
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string ReadinessLevel { get; init; }

    public required int ExpectedScoreMinimum { get; init; }

    public required int ExpectedScoreMaximum { get; init; }

    public required IReadOnlyList<string> Categories { get; init; }

    public required int ProjectCount { get; init; }
}
