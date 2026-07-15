using Forge.Api.Analysis;
using Forge.Api.Models;

namespace Forge.Api.Services;

public static class BundledSampleCatalog
{
    public const string DefaultSampleId = "sample-shop";

    private static readonly IReadOnlyList<BundledSampleDefinition> Samples =
    [
        new(
            DefaultSampleId,
            "Sample Shop",
            "A layered commerce API with intentional architecture, DI, EF Core, security, and API-readiness problems.",
            "Medium",
            82,
            92,
            ["Architecture", "Dependency Injection", "EF Core", "Security", "API Readiness"],
            4,
            "Forge.SampleShop",
            "Forge.SampleShop.slnx"),
        new(
            "clean-minimal-api",
            "Clean Minimal API",
            "A compact order-tracking API with production middleware, health checks, OpenAPI, validation, and clean project boundaries.",
            "High",
            88,
            100,
            ["Architecture", "Dependency Injection", "Security", "API Readiness"],
            3,
            "CleanMinimalApi",
            "CleanMinimalApi.slnx"),
        new(
            "risky-minimal-api",
            "Risky Minimal API",
            "A functioning inventory API that omits several production safeguards and contains placeholder-only insecure configuration.",
            "Low",
            78,
            86,
            ["Architecture", "Dependency Injection", "EF Core", "Security", "API Readiness"],
            4,
            "RiskyMinimalApi",
            "RiskyMinimalApi.slnx"),
        new(
            "mvc-web-ui-no-swagger",
            "MVC Web UI, No Swagger",
            "A server-rendered support portal that demonstrates why MVC and Razor applications should not be treated as APIs.",
            "High",
            82,
            100,
            ["Architecture", "Dependency Injection", "Security", "API Readiness"],
            3,
            "MvcWebUiNoSwagger",
            "MvcWebUiNoSwagger.slnx"),
        new(
            "bad-ef-migration",
            "Destructive EF Migration",
            "A reservation API with a realistic EF Core model and one intentionally destructive migration operation in Up().",
            "Medium",
            80,
            92,
            ["EF Core", "Dependency Injection", "API Readiness"],
            3,
            "BadEfMigration",
            "BadEfMigration.slnx"),
        new(
            "missing-di-registration",
            "Missing DI Registration",
            "A notification API with one custom service omitted from the composition root alongside valid framework-provided dependencies.",
            "Medium",
            80,
            92,
            ["Dependency Injection", "Security", "API Readiness"],
            3,
            "MissingDiRegistration",
            "MissingDiRegistration.slnx")
    ];

    public static IReadOnlyList<BundledSampleSummary> List()
    {
        return Samples.Select(ToSummary).ToArray();
    }

    public static BundledSampleDefinition Get(string? sampleId)
    {
        var normalizedId = string.IsNullOrWhiteSpace(sampleId) ? DefaultSampleId : sampleId.Trim();
        return Samples.FirstOrDefault(sample => sample.Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("The selected sample is not available.", nameof(sampleId));
    }

    public static BundledSampleDefinition? FindBySolutionName(string? solutionName)
    {
        return Samples.FirstOrDefault(sample =>
            sample.SolutionName.Equals(solutionName, StringComparison.OrdinalIgnoreCase)
            || sample.Name.Equals(solutionName, StringComparison.OrdinalIgnoreCase));
    }

    public static string ResolveSolutionPath(string? sampleId = null)
    {
        var sample = Get(sampleId);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "samples",
                sample.DirectoryName,
                sample.SolutionFileName);

            if (File.Exists(candidate))
            {
                return AnalyzerUtilities.NormalizePath(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("The selected bundled sample could not be found.");
    }

    private static BundledSampleSummary ToSummary(BundledSampleDefinition sample)
    {
        return new BundledSampleSummary
        {
            Id = sample.Id,
            Name = sample.Name,
            Description = sample.Description,
            ReadinessLevel = sample.ReadinessLevel,
            ExpectedScoreMinimum = sample.ExpectedScoreMinimum,
            ExpectedScoreMaximum = sample.ExpectedScoreMaximum,
            Categories = sample.Categories,
            ProjectCount = sample.ProjectCount
        };
    }
}

public sealed record BundledSampleDefinition(
    string Id,
    string Name,
    string Description,
    string ReadinessLevel,
    int ExpectedScoreMinimum,
    int ExpectedScoreMaximum,
    IReadOnlyList<string> Categories,
    int ProjectCount,
    string DirectoryName,
    string SolutionFileName)
{
    public string SolutionName => Path.GetFileNameWithoutExtension(SolutionFileName);
}
