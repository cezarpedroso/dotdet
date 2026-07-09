using Forge.Api.Analysis;
using Forge.Api.Analyzers;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Forge.Api.Tests;

public sealed class SemanticAnalysisTests
{
    [Fact]
    public void RuleCatalog_ContainsEnrichedAnalyzerDocumentation()
    {
        var catalog = new RuleCatalogService(new IssueEnrichmentService()).GetRules();

        Assert.Equal(30, catalog.Count);
        Assert.Contains(catalog, rule =>
            rule.RuleId == "DI003"
            && rule.DetectionMethod == IssueEnrichmentService.RoslynSemanticAnalysis
            && rule.Confidence == IssueConfidence.High
            && !string.IsNullOrWhiteSpace(rule.DetectionLogic)
            && !string.IsNullOrWhiteSpace(rule.FalsePositiveGuidance)
            && !string.IsNullOrWhiteSpace(rule.GoodExample)
            && !string.IsNullOrWhiteSpace(rule.BadExample)
            && rule.DocumentationLinks.Count > 0);
        Assert.Contains(catalog, rule =>
            rule.RuleId == "API005"
            && !string.IsNullOrWhiteSpace(rule.SuggestedCodeSnippet)
            && rule.RelatedRules.Count > 0);
    }

    [Fact]
    public void SuppressionService_CreatesAndRemovesRepositorySuppressionFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-suppressions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var solutionPath = Path.Combine(tempRoot, "Sample.sln");
        File.WriteAllText(solutionPath, string.Empty);

        try
        {
            var service = new SuppressionService();
            var suppression = service.Create(new CreateSuppressionInput(
                solutionPath,
                "SEC002",
                Path.Combine(tempRoot, "src", "Api", "Program.cs"),
                "Sample.Api",
                "Documented browser-only public endpoint.",
                "Accepted Risk",
                DateTimeOffset.UtcNow.AddDays(30)));

            var suppressionFilePath = Path.Combine(tempRoot, SuppressionService.SuppressionFileName);
            Assert.True(File.Exists(suppressionFilePath));

            var loaded = service.Load(solutionPath);
            Assert.Contains(loaded.Suppressions, item =>
                item.Id == suppression.Id
                && item.File == "src/Api/Program.cs"
                && item.Status == "Accepted Risk");

            Assert.True(service.Remove(solutionPath, suppression.Id));
            Assert.Empty(service.Load(solutionPath).Suppressions);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AnalyzerUtilities_InferCommonCleanArchitectureLayers()
    {
        Assert.Equal(AnalyzerUtilities.PresentationLayer, AnalyzerUtilities.InferLogicalLayer("eShopOnWeb.Web", isWebProject: false, isTestProject: false));
        Assert.Equal(AnalyzerUtilities.PresentationLayer, AnalyzerUtilities.InferLogicalLayer("eShopOnWeb.PublicApi", isWebProject: false, isTestProject: false));
        Assert.Equal(AnalyzerUtilities.PresentationLayer, AnalyzerUtilities.InferLogicalLayer("eShopOnWeb.BlazorAdmin", isWebProject: false, isTestProject: false));
        Assert.Equal(AnalyzerUtilities.DomainLayer, AnalyzerUtilities.InferLogicalLayer("eShopOnWeb.ApplicationCore", isWebProject: false, isTestProject: false));
        Assert.Equal(AnalyzerUtilities.DomainLayer, AnalyzerUtilities.InferLogicalLayer("eShopOnWeb.Core", isWebProject: false, isTestProject: false));
        Assert.Equal(AnalyzerUtilities.InfrastructureLayer, AnalyzerUtilities.InferLogicalLayer("eShopOnWeb.Infrastructure", isWebProject: false, isTestProject: false));
        Assert.Equal(AnalyzerUtilities.TestLayer, AnalyzerUtilities.InferLogicalLayer("eShopOnWeb.FunctionalTests", isWebProject: true, isTestProject: true));
        Assert.True(AnalyzerUtilities.HasTestFrameworkReference(["Microsoft.NET.Test.Sdk"]));
    }

    [Fact]
    public void ScoringService_UsesWeightedCategoryScoresAndCriticalCaps()
    {
        var service = new ScoringService();
        var categoryScores = new CategoryScores
        {
            Architecture = 100,
            Security = 72,
            EfCore = 78,
            DependencyInjection = 76,
            ApiReadiness = 76
        };

        Assert.InRange(service.CalculateOverallScore(categoryScores, []), 78, 80);

        var twoCriticals = Enumerable.Range(1, 2)
            .Select(index => CreateIssue($"CRIT-{index}", IssueSeverity.Critical))
            .ToArray();
        var sixCriticals = Enumerable.Range(1, 6)
            .Select(index => CreateIssue($"CRIT-{index}", IssueSeverity.Critical))
            .ToArray();
        var nineCriticals = Enumerable.Range(1, 9)
            .Select(index => CreateIssue($"CRIT-{index}", IssueSeverity.Critical))
            .ToArray();

        Assert.Equal(82, service.CalculateOverallScore(new CategoryScores { Architecture = 95, Security = 95, EfCore = 95, DependencyInjection = 95, ApiReadiness = 95 }, twoCriticals));
        Assert.Equal(68, service.CalculateOverallScore(new CategoryScores { Architecture = 95, Security = 95, EfCore = 95, DependencyInjection = 95, ApiReadiness = 95 }, sixCriticals));
        Assert.Equal(49, service.CalculateOverallScore(new CategoryScores { Architecture = 95, Security = 95, EfCore = 95, DependencyInjection = 95, ApiReadiness = 95 }, nineCriticals));
    }

    [Fact]
    public async Task AnalyzeAsync_ExcludesTestProjectsAndScoresProductionRulesRealistically()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-eshop-style-{Guid.NewGuid():N}");
        var solutionPath = Path.Combine(tempRoot, "eShopOnWeb.slnx");
        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteFile(solutionPath, """
                <Solution>
                  <Folder Name="/src/">
                    <Project Path="src/eShopOnWeb.Web/eShopOnWeb.Web.csproj" />
                    <Project Path="src/eShopOnWeb.ApplicationCore/eShopOnWeb.ApplicationCore.csproj" />
                    <Project Path="src/eShopOnWeb.Infrastructure/eShopOnWeb.Infrastructure.csproj" />
                  </Folder>
                  <Folder Name="/tests/">
                    <Project Path="tests/eShopOnWeb.FunctionalTests/eShopOnWeb.FunctionalTests.csproj" />
                  </Folder>
                </Solution>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "eShopOnWeb.Web", "eShopOnWeb.Web.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
                    <ProjectReference Include="..\eShopOnWeb.ApplicationCore\eShopOnWeb.ApplicationCore.csproj" />
                  </ItemGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "eShopOnWeb.Web", "Program.cs"), """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllers();
                builder.Services.AddOpenApi();
                var app = builder.Build();
                app.MapOpenApi();
                app.UseAuthorization();
                app.MapControllers();
                app.Run();
                """);
            WriteFile(Path.Combine(tempRoot, "src", "eShopOnWeb.Web", "Controllers", "CatalogController.cs"), """
                using Microsoft.AspNetCore.Mvc;

                namespace eShopOnWeb.Web.Controllers;

                [ApiController]
                [Route("api/catalog")]
                public sealed class CatalogController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult Get() => Ok();
                }
                """);
            WriteFile(Path.Combine(tempRoot, "src", "eShopOnWeb.Web", "appsettings.json"), """
                {
                  "ConnectionStrings": {
                    "CatalogConnection": "Server=(localdb)\\mssqllocaldb;Database=CatalogDb;Trusted_Connection=True;"
                  }
                }
                """);
            WriteFile(Path.Combine(tempRoot, "src", "eShopOnWeb.Web", "appsettings.Development.json"), """
                {
                  "ConnectionStrings": {
                    "CatalogConnection": "Server=prod.database.windows.net;Database=CatalogDb;User Id=dev;Password=dev-password;"
                  }
                }
                """);
            WriteFile(Path.Combine(tempRoot, "src", "eShopOnWeb.ApplicationCore", "eShopOnWeb.ApplicationCore.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "eShopOnWeb.ApplicationCore", "CatalogItem.cs"), """
                namespace eShopOnWeb.ApplicationCore;

                public sealed class CatalogItem
                {
                    public int Id { get; set; }
                }
                """);
            WriteFile(Path.Combine(tempRoot, "src", "eShopOnWeb.Infrastructure", "eShopOnWeb.Infrastructure.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
                  </ItemGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "eShopOnWeb.Infrastructure", "Migrations", "202607080001_DropLegacy.cs"), """
                namespace eShopOnWeb.Infrastructure.Migrations;

                public sealed class DropLegacy
                {
                    public void Up(dynamic migrationBuilder)
                    {
                        migrationBuilder.DropTable("LegacyItems");
                    }
                }
                """);
            WriteFile(Path.Combine(tempRoot, "tests", "eShopOnWeb.FunctionalTests", "eShopOnWeb.FunctionalTests.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
                    <ProjectReference Include="..\..\src\eShopOnWeb.Web\eShopOnWeb.Web.csproj" />
                  </ItemGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "tests", "eShopOnWeb.FunctionalTests", "Program.cs"), """
                var builder = WebApplication.CreateBuilder(args);
                var app = builder.Build();
                app.Run();
                """);
            WriteFile(Path.Combine(tempRoot, "tests", "eShopOnWeb.FunctionalTests", "appsettings.json"), """
                {
                  "ConnectionStrings": {
                    "FunctionalTestConnection": "Server=prod.example;Database=Tests;User Id=sa;Password=test-password;"
                  }
                }
                """);

            var result = await CreateService().AnalyzeAsync(solutionPath, CancellationToken.None);

            Assert.Contains(result.ProjectGraph.Projects, project =>
                project.Name == "eShopOnWeb.FunctionalTests"
                && project.IsTestProject
                && project.LogicalLayer == AnalyzerUtilities.TestLayer);
            Assert.Contains(result.ProjectGraph.Projects, project =>
                project.Name == "eShopOnWeb.ApplicationCore"
                && project.LogicalLayer == AnalyzerUtilities.DomainLayer);
            Assert.Contains(result.ProjectGraph.Projects, project =>
                project.Name == "eShopOnWeb.Web"
                && project.IsAspNetCoreEntryPoint
                && project.LogicalLayer == AnalyzerUtilities.PresentationLayer);

            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "API005"
                && issue.ProjectName == "eShopOnWeb.Web");
            Assert.DoesNotContain(result.Issues, issue =>
                issue.RuleId == "API005"
                && issue.ProjectName != "eShopOnWeb.Web");
            Assert.DoesNotContain(result.Issues, issue =>
                issue.ProjectName == "eShopOnWeb.FunctionalTests");
            Assert.DoesNotContain(result.Issues, issue =>
                issue.RuleId == "SECCONN"
                && issue.Severity == IssueSeverity.Error);
            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "SECCONN"
                && issue.FilePath?.EndsWith("appsettings.Development.json", StringComparison.OrdinalIgnoreCase) == true
                && issue.Severity == IssueSeverity.Warning);
            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "SEC004"
                && issue.ProjectName == "eShopOnWeb.Web"
                && issue.Severity == IssueSeverity.Info
                && issue.Confidence == IssueConfidence.Low);
            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "EF004"
                && issue.ProjectName == "eShopOnWeb.Infrastructure"
                && issue.Severity == IssueSeverity.Warning);
            Assert.InRange(result.OverallScore, 70, 100);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_JwtPackageWithoutAuthenticationMiddlewareEscalatesOnlyForProtectedApis()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-jwt-severity-{Guid.NewGuid():N}");
        var solutionPath = Path.Combine(tempRoot, "JwtSeverity.slnx");
        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteFile(solutionPath, """
                <Solution>
                  <Project Path="src/PublicApi/PublicApi.csproj" />
                  <Project Path="src/ProtectedApi/ProtectedApi.csproj" />
                </Solution>
                """);
            WriteJwtProject(tempRoot, "PublicApi", protectedEndpoint: false);
            WriteJwtProject(tempRoot, "ProtectedApi", protectedEndpoint: true);

            var result = await CreateService().AnalyzeAsync(solutionPath, CancellationToken.None);

            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "SEC004"
                && issue.ProjectName == "PublicApi"
                && issue.Severity == IssueSeverity.Info
                && issue.Confidence == IssueConfidence.Low);
            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "SEC004"
                && issue.ProjectName == "ProtectedApi"
                && issue.Severity == IssueSeverity.Error
                && issue.Confidence == IssueConfidence.High);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_DiMissingRegistrationOnlyReportsContainerActivatedOwners()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-di-applicability-{Guid.NewGuid():N}");
        var solutionPath = Path.Combine(tempRoot, "DiApplicability.slnx");
        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteFile(solutionPath, """
                <Solution>
                  <Project Path="src/DiApplicability.Api/DiApplicability.Api.csproj" />
                </Solution>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "DiApplicability.Api", "DiApplicability.Api.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "DiApplicability.Api", "Program.cs"), """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllers();
                builder.Services.AddScoped<RegisteredWorkflow>();
                var app = builder.Build();
                app.MapControllers();
                app.Run();
                """);
            WriteFile(Path.Combine(tempRoot, "src", "DiApplicability.Api", "Services.cs"), """
                public interface IInternalDependency { }
                public interface IRegisteredDependency { }
                public interface IControllerDependency { }

                public sealed class OrdinaryHelper
                {
                    public OrdinaryHelper(IInternalDependency dependency) { }
                }

                public sealed class RegisteredWorkflow
                {
                    public RegisteredWorkflow(IRegisteredDependency dependency) { }
                }
                """);
            WriteFile(Path.Combine(tempRoot, "src", "DiApplicability.Api", "Controllers", "SampleController.cs"), """
                using Microsoft.AspNetCore.Mvc;

                public sealed class SampleController : ControllerBase
                {
                    public SampleController(IControllerDependency dependency) { }
                }
                """);

            var result = await CreateService().AnalyzeAsync(solutionPath, CancellationToken.None);

            Assert.DoesNotContain(result.Issues, issue =>
                issue.RuleId == "DI002"
                && issue.Description.Contains("IInternalDependency", StringComparison.Ordinal));
            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "DI002"
                && issue.Description.Contains("IRegisteredDependency", StringComparison.Ordinal)
                && issue.WhyDetected?.Contains("Constructor owner appears to be activated by dependency injection.", StringComparison.Ordinal) == true);
            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "DI002"
                && issue.Description.Contains("IControllerDependency", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_EfPrimaryKeyRuleHonorsInheritedAndConfiguredKeys()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-ef-keys-{Guid.NewGuid():N}");
        var solutionPath = Path.Combine(tempRoot, "EfKeys.slnx");
        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteFile(solutionPath, """
                <Solution>
                  <Project Path="src/EfKeys.Infrastructure/EfKeys.Infrastructure.csproj" />
                </Solution>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "EfKeys.Infrastructure", "EfKeys.Infrastructure.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
                  </ItemGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "EfKeys.Infrastructure", "CatalogDbContext.cs"), """
                using Microsoft.EntityFrameworkCore;

                public sealed class CatalogDbContext : DbContext
                {
                    public DbSet<InheritedKeyEntity> InheritedKeyEntities => Set<InheritedKeyEntity>();
                    public DbSet<ConfiguredKeyEntity> ConfiguredKeyEntities => Set<ConfiguredKeyEntity>();
                    public DbSet<KeylessReport> KeylessReports => Set<KeylessReport>();
                    public DbSet<MissingKeyEntity> MissingKeyEntities => Set<MissingKeyEntity>();

                    protected override void OnModelCreating(ModelBuilder modelBuilder)
                    {
                        modelBuilder.Entity<ConfiguredKeyEntity>().HasKey(entity => entity.Sku);
                        modelBuilder.Entity<KeylessReport>().HasNoKey();
                    }
                }

                public abstract class BaseEntity
                {
                    public int Id { get; set; }
                }

                public sealed class InheritedKeyEntity : BaseEntity
                {
                    public string Name { get; set; } = "";
                }

                public sealed class ConfiguredKeyEntity
                {
                    public string Sku { get; set; } = "";
                }

                public sealed class KeylessReport
                {
                    public string Label { get; set; } = "";
                }

                public sealed class MissingKeyEntity
                {
                    public string Label { get; set; } = "";
                }
                """);

            var result = await CreateService().AnalyzeAsync(solutionPath, CancellationToken.None);

            Assert.DoesNotContain(result.Issues, issue =>
                issue.RuleId == "EF003"
                && (issue.Description.Contains("InheritedKeyEntity", StringComparison.Ordinal)
                    || issue.Description.Contains("ConfiguredKeyEntity", StringComparison.Ordinal)
                    || issue.Description.Contains("KeylessReport", StringComparison.Ordinal)));
            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "EF003"
                && issue.Description.Contains("MissingKeyEntity", StringComparison.Ordinal)
                && issue.WhyDetected?.StartsWith("Evidence:", StringComparison.Ordinal) == true);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_FindsSemanticIssuesInSampleSolution()
    {
        var service = CreateService();

        var result = await service.AnalyzeAsync(GetSampleSolutionPath(), CancellationToken.None);

        Assert.Equal(GetSampleSolutionPath(), result.SolutionPath);
        Assert.Contains(result.SourceFiles, file =>
            file.RelativePath.EndsWith("src/Forge.SampleShop.Api/Program.cs", StringComparison.OrdinalIgnoreCase)
            && file.Content.Contains("using Forge.SampleShop.Application.Orders;", StringComparison.Ordinal)
            && file.Content.Contains("builder.Services.AddCors", StringComparison.Ordinal)
            && file.Language == "csharp");

        Assert.Contains(result.Issues, issue =>
            issue.Category == AnalysisCategories.Architecture
            && issue.RuleId == "ARCH003"
            && issue.Title == "Application layer references Infrastructure directly"
            && issue.LineNumber is > 0);

        Assert.Contains(result.Issues, issue =>
            issue.Category == AnalysisCategories.DependencyInjection
            && issue.Title == "Duplicate dependency injection registration"
            && issue.FilePath?.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase) == true
            && issue.LineNumber is > 0);

        Assert.Contains(result.Issues, issue =>
            issue.Category == AnalysisCategories.DependencyInjection
            && issue.RuleId == "DI003"
            && issue.Title == "Singleton service captures scoped dependency"
            && issue.Confidence == IssueConfidence.High
            && issue.DetectionMethod == IssueEnrichmentService.RoslynSemanticAnalysis
            && !string.IsNullOrWhiteSpace(issue.GoodExample)
            && !string.IsNullOrWhiteSpace(issue.BadExample));

        Assert.Contains(result.Issues, issue =>
            issue.Category == AnalysisCategories.DependencyInjection
            && issue.Title == "Constructor dependency appears unregistered"
            && issue.Description.Contains("IPaymentGateway", StringComparison.Ordinal)
            && issue.LineNumber is > 0);

        Assert.Contains(result.Issues, issue =>
            issue.Category == AnalysisCategories.EfCore
            && issue.Title == "Entity is missing an obvious primary key"
            && issue.Description.Contains("CatalogItem", StringComparison.Ordinal)
            && issue.LineNumber is > 0);

        Assert.Contains(result.Issues, issue =>
            issue.Category == AnalysisCategories.Security
            && issue.Title == "CORS policy allows any origin"
            && issue.FilePath?.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase) == true
            && issue.LineNumber is > 0);

        Assert.Contains(result.Issues, issue =>
            issue.Category == AnalysisCategories.ApiReadiness
            && issue.Title == "Global exception handling is missing");

        Assert.Contains(result.Issues, issue =>
            issue.RuleId == "SEC004"
            && issue.Confidence is IssueConfidence.Low
            && !string.IsNullOrWhiteSpace(issue.DetectionMethod)
            && !string.IsNullOrWhiteSpace(issue.ProblemSummary)
            && issue.WhyDetected?.StartsWith("Evidence:", StringComparison.Ordinal) == true
            && !string.IsNullOrWhiteSpace(issue.WhyItMatters)
            && !string.IsNullOrWhiteSpace(issue.RecommendedPattern)
            && !string.IsNullOrWhiteSpace(issue.SuggestedImplementation)
            && !string.IsNullOrWhiteSpace(issue.SuggestedSnippet)
            && !string.IsNullOrWhiteSpace(issue.GoodExample)
            && !string.IsNullOrWhiteSpace(issue.BadExample)
            && issue.DocumentationLinks is { Count: > 0 });

        Assert.True(result.SuppressionCount >= 1);
        Assert.Contains(result.Issues, issue =>
            issue.RuleId == "SEC003"
            && issue.Suppression is
            {
                Id: "sup_sample_https_redirection",
                Status: "Accepted Risk",
                IsExpired: false
            });

        Assert.Contains(result.ProjectGraph.Dependencies, dependency =>
            dependency.SourceProjectName == "Forge.SampleShop.Api"
            && dependency.TargetProjectName == "Forge.SampleShop.Application");

        Assert.NotNull(result.ArchitectureMap);
        Assert.Contains(result.ArchitectureMap.Projects, project =>
            project.Name == "Forge.SampleShop.Application"
            && project.Layer == "Application");
        Assert.Contains(result.ArchitectureMap.Dependencies, dependency =>
            dependency.SourceProjectName == "Forge.SampleShop.Application"
            && dependency.TargetProjectName == "Forge.SampleShop.Infrastructure"
            && dependency.IsViolation
            && !string.IsNullOrWhiteSpace(dependency.RelatedFindingId));
        Assert.Contains(result.ArchitectureMap.Dependencies, dependency =>
            dependency.SourceProjectName == "Forge.SampleShop.Api"
            && dependency.TargetProjectName == "Forge.SampleShop.Application"
            && !dependency.IsViolation
            && string.IsNullOrWhiteSpace(dependency.RelatedFindingId));
        Assert.NotEmpty(result.ArchitectureMap.Violations);

        Assert.NotNull(result.EngineeringAssessment);
        Assert.False(string.IsNullOrWhiteSpace(result.EngineeringAssessment.OverallProductionReadiness));
        Assert.NotEmpty(result.EngineeringAssessment.HighestRisks);
        Assert.NotEmpty(result.EngineeringAssessment.RecommendedPriorities);
    }

    private static SolutionAnalysisService CreateService()
    {
        var semanticHelper = new SemanticAnalysisHelper();

        return new SolutionAnalysisService(
            new ArchitectureAnalyzer(semanticHelper),
            new DependencyInjectionAnalyzer(),
            new EfCoreAnalyzer(semanticHelper),
            new SecurityConfigurationAnalyzer(semanticHelper),
            new ApiReadinessAnalyzer(semanticHelper),
            new IssueEnrichmentService(),
            new ScoringService(),
            new ArchitectureMapService(),
            new EngineeringAssessmentService(),
            new SuppressionService(),
            NullLogger<SolutionAnalysisService>.Instance);
    }

    private static string GetSampleSolutionPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "samples",
            "Forge.SampleShop",
            "Forge.SampleShop.slnx"));
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory());
        File.WriteAllText(path, content);
    }

    private static AnalysisIssue CreateIssue(string id, IssueSeverity severity)
    {
        return new AnalysisIssue
        {
            Id = id,
            RuleId = id,
            Title = id,
            Description = id,
            Severity = severity,
            Category = AnalysisCategories.Security,
            Recommendation = id
        };
    }

    private static void WriteJwtProject(string root, string projectName, bool protectedEndpoint)
    {
        WriteFile(Path.Combine(root, "src", projectName, $"{projectName}.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
              </ItemGroup>
            </Project>
            """);
        WriteFile(Path.Combine(root, "src", projectName, "Program.cs"), protectedEndpoint
            ? """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllers();
                builder.Services.AddAuthorization();
                var app = builder.Build();
                app.UseAuthorization();
                app.MapControllers();
                app.Run();
                """
            : """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllers();
                var app = builder.Build();
                app.MapControllers();
                app.Run();
                """);
        WriteFile(Path.Combine(root, "src", projectName, "Controllers", "SampleController.cs"), protectedEndpoint
            ? """
                using Microsoft.AspNetCore.Authorization;
                using Microsoft.AspNetCore.Mvc;

                [ApiController]
                [Route("api/sample")]
                [Authorize]
                public sealed class SampleController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult Get() => Ok();
                }
                """
            : """
                using Microsoft.AspNetCore.Mvc;

                [ApiController]
                [Route("api/sample")]
                public sealed class SampleController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult Get() => Ok();
                }
                """);
    }
}
