using Forge.Api.Analysis;
using Forge.Api.Analyzers;
using Forge.Api.Contracts;
using Forge.Api.Controllers;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Security.Claims;
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
    public void SuppressionService_LoadsRepositorySuppressionFileReadOnly()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-suppressions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var solutionPath = Path.Combine(tempRoot, "Sample.sln");
        File.WriteAllText(solutionPath, string.Empty);
        var suppressionFilePath = Path.Combine(tempRoot, SuppressionService.SuppressionFileName);
        File.WriteAllText(suppressionFilePath, """
            {
              "version": 1,
              "suppressions": [
                {
                  "id": "sup_local_dev",
                  "ruleId": "SEC002",
                  "file": "src/Api/Program.cs",
                  "project": "Sample.Api",
                  "reason": "Documented browser-only public endpoint.",
                  "status": "Accepted Risk",
                  "createdDate": "2026-01-01T00:00:00+00:00",
                  "expiration": "2027-01-01T00:00:00+00:00"
                }
              ]
            }
            """);

        try
        {
            var service = new SuppressionService();
            var loaded = service.Load(solutionPath);
            Assert.Contains(loaded.Suppressions, item =>
                item.Id == "sup_local_dev"
                && item.File == "src/Api/Program.cs"
                && item.Status == "Accepted Risk");
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
    public void EngineeringAssessment_AlwaysProvidesScoreExplanation()
    {
        var service = new EngineeringAssessmentService();
        var assessment = service.Build(
            78,
            new CategoryScores
            {
                Architecture = 100,
                Security = 72,
                EfCore = 78,
                DependencyInjection = 76,
                ApiReadiness = 76
            },
            [CreateIssue("SEC004", IssueSeverity.Warning)],
            CreateEmptyArchitectureMap());

        Assert.False(string.IsNullOrWhiteSpace(assessment.ScoreExplanation));
        Assert.DoesNotContain("undefined", assessment.ScoreExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", assessment.ScoreExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("weighted category scores", assessment.ScoreExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EngineeringAssessment_DeduplicatesDiRootCauseFamilies()
    {
        var service = new EngineeringAssessmentService();
        var issues = new[]
        {
            CreateIssue("DI001-A", "DI001", "Duplicate registrations for ICatalogViewModelService", "ICatalogViewModelService is registered twice.", IssueSeverity.Warning, AnalysisCategories.DependencyInjection, "Web", "Program.cs", 15) with
            {
                RootCauseKey = "DI001|Web|ICatalogViewModelService"
            },
            CreateIssue("DI001-B", "DI001", "Duplicate registrations for IOrderService", "IOrderService is registered twice.", IssueSeverity.Warning, AnalysisCategories.DependencyInjection, "Web", "Program.cs", 20) with
            {
                RootCauseKey = "DI001|Web|IOrderService"
            },
            CreateIssue("DI002-A", "DI002", "HttpClient appears unregistered", "HttpClient appears unregistered.", IssueSeverity.Warning, AnalysisCategories.DependencyInjection, "BlazorAdmin", "CustomAuthStateProvider.cs", 25) with
            {
                RootCauseKey = "DI002|BlazorAdmin|HttpClient"
            },
            CreateIssue("DI002-B", "DI002", "IEmailSender appears unregistered", "IEmailSender appears unregistered.", IssueSeverity.Warning, AnalysisCategories.DependencyInjection, "BlazorAdmin", "CheckoutService.cs", 42) with
            {
                RootCauseKey = "DI002|BlazorAdmin|IEmailSender"
            }
        };

        var assessment = service.Build(
            76,
            new CategoryScores
            {
                Architecture = 100,
                Security = 100,
                EfCore = 100,
                DependencyInjection = 76,
                ApiReadiness = 100
            },
            issues,
            CreateEmptyArchitectureMap());

        Assert.Equal(1, CountOccurrences(assessment.ScoreExplanation, "duplicate DI registrations"));
        Assert.DoesNotContain("DI001: Duplicate dependency injection registration; DI001", assessment.ScoreExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DI002: Constructor dependency appears unregistered; DI002", assessment.ScoreExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.Single(assessment.RecommendedPriorities, priority => priority.StartsWith("DI001:", StringComparison.Ordinal));
        Assert.Single(assessment.RecommendedPriorities, priority => priority.StartsWith("DI002:", StringComparison.Ordinal));
    }

    [Fact]
    public void FindingGroupingService_GroupsNoisyRootCauseFindingsWithEvidence()
    {
        var groupingService = new FindingGroupingService();
        var issues = new[]
        {
            CreateIssue("EF004-A", "EF004", "Migration contains destructive schema operation", "InitialCreate.cs calls migrationBuilder.DropColumn.", IssueSeverity.Warning, AnalysisCategories.EfCore, "Store.Infrastructure", "Migrations/InitialCreate.cs", 176),
            CreateIssue("EF004-B", "EF004", "Migration contains destructive schema operation", "InitialCreate.cs calls migrationBuilder.DropForeignKey.", IssueSeverity.Warning, AnalysisCategories.EfCore, "Store.Infrastructure", "Migrations/InitialCreate.cs", 179),
            CreateIssue("EF004-C", "EF004", "Migration contains destructive schema operation", "InitialCreate.cs calls migrationBuilder.DropIndex.", IssueSeverity.Warning, AnalysisCategories.EfCore, "Store.Infrastructure", "Migrations/InitialCreate.cs", 182),
            CreateIssue("EF005-A", "EF005", "Migration executes raw SQL", "InitialCreate.cs calls migrationBuilder.Sql.", IssueSeverity.Warning, AnalysisCategories.EfCore, "Store.Infrastructure", "Migrations/InitialCreate.cs", 190),
            CreateIssue("EF005-B", "EF005", "Migration executes raw SQL", "InitialCreate.cs calls migrationBuilder.Sql.", IssueSeverity.Warning, AnalysisCategories.EfCore, "Store.Infrastructure", "Migrations/InitialCreate.cs", 194),
            CreateIssue("DI001-A", "DI001", "Duplicate dependency injection registration", "ICatalogService is registered 2 times in startup or composition code.", IssueSeverity.Warning, AnalysisCategories.DependencyInjection, "Store.Web", "Program.cs", 22),
            CreateIssue("DI001-B", "DI001", "Duplicate dependency injection registration", "ICatalogService is registered 2 times in startup or composition code.", IssueSeverity.Warning, AnalysisCategories.DependencyInjection, "Store.Web", "ConfigureServices.cs", 15),
            CreateIssue("DI002-A", "DI002", "Constructor dependency appears unregistered", "OrderController injects IEmailSender, but DotDet did not find a matching service registration.", IssueSeverity.Warning, AnalysisCategories.DependencyInjection, "Store.Web", "Controllers/OrderController.cs", 17),
            CreateIssue("DI002-B", "DI002", "Constructor dependency appears unregistered", "CheckoutService injects IEmailSender, but DotDet did not find a matching service registration.", IssueSeverity.Warning, AnalysisCategories.DependencyInjection, "Store.Web", "Services/CheckoutService.cs", 42),
            CreateIssue("SECCONN-A", "SECCONN", "Connection string is stored in configuration", "appsettings.Docker.json contains a connection string value for ConnectionStrings:CatalogConnection.", IssueSeverity.Warning, AnalysisCategories.Security, "Store.Web", "appsettings.Docker.json", 3),
            CreateIssue("SECCONN-B", "SECCONN", "Connection string is stored in configuration", "appsettings.Docker.json contains a connection string value for ConnectionStrings:IdentityConnection.", IssueSeverity.Warning, AnalysisCategories.Security, "Store.Web", "appsettings.Docker.json", 4),
        };

        var grouped = groupingService.Group(issues);

        var ef004 = Assert.Single(grouped, issue => issue.RuleId == "EF004");
        Assert.Equal("Migration contains destructive schema operations", ef004.Title);
        Assert.Equal(3, ef004.Evidence?.Count);
        Assert.Contains(ef004.Evidence!, item => item.Label == "DropForeignKey" && item.LineNumber == 179);
        Assert.Contains(ef004.Evidence!, item => item.Label == "DropIndex" && item.LineNumber == 182);
        Assert.False(string.IsNullOrWhiteSpace(ef004.RootCauseKey));

        var ef005 = Assert.Single(grouped, issue => issue.RuleId == "EF005");
        Assert.Equal(2, ef005.Evidence?.Count);

        var di001 = Assert.Single(grouped, issue => issue.RuleId == "DI001");
        Assert.Equal("Duplicate registrations for ICatalogService", di001.Title);
        Assert.Equal(2, di001.Evidence?.Count);

        var di002 = Assert.Single(grouped, issue => issue.RuleId == "DI002");
        Assert.Equal("IEmailSender appears unregistered", di002.Title);
        Assert.Equal(2, di002.Evidence?.Count);

        var connectionStrings = Assert.Single(grouped, issue => issue.RuleId == "SECCONN");
        Assert.Equal("Connection strings found in committed configuration", connectionStrings.Title);
        Assert.Equal(2, connectionStrings.Evidence?.Count);
        Assert.Contains(connectionStrings.Evidence!, item => item.Label == "ConnectionStrings:CatalogConnection");
        Assert.Contains(connectionStrings.Evidence!, item => item.Label == "ConnectionStrings:IdentityConnection");
    }

    [Fact]
    public void ScoringService_UsesRootCauseKeysForRepeatedEvidence()
    {
        var service = new ScoringService();
        var repeatedMigrationOperations = Enumerable.Range(1, 6)
            .Select(index => CreateIssue($"EF004-{index}", "EF004", "Migration contains destructive schema operation", $"InitialCreate.cs calls migrationBuilder.DropColumn at {index}.", IssueSeverity.Warning, AnalysisCategories.EfCore, "Store.Infrastructure", "Migrations/InitialCreate.cs", 100 + index) with
            {
                RootCauseKey = "EF004|Store.Infrastructure|Migrations/InitialCreate.cs"
            })
            .ToArray();

        var categoryScores = service.CalculateCategoryScores(repeatedMigrationOperations);
        Assert.InRange(categoryScores.EfCore, 90, 96);
        Assert.InRange(service.CalculateOverallScore(categoryScores, repeatedMigrationOperations), 95, 99);
    }

    [Fact]
    public void EfCoreAnalyzer_IgnoresDestructiveMigrationOperationsInDownMethod()
    {
        var issues = AnalyzeMigrationSource("""
            public sealed class InitialCreate
            {
                protected void Up(dynamic migrationBuilder)
                {
                }

                protected void Down(dynamic migrationBuilder)
                {
                    migrationBuilder.DropTable("Orders");
                    migrationBuilder.DropColumn("LegacyId", "Orders");
                    migrationBuilder.Sql("delete from Orders");
                }
            }
            """);

        Assert.DoesNotContain(issues, issue => issue.RuleId is "EF004" or "EF005");
    }

    [Fact]
    public void EfCoreAnalyzer_ReportsRiskyMigrationOperationsInUpMethod()
    {
        var issues = AnalyzeMigrationSource("""
            public sealed class DropLegacyColumns
            {
                protected void Up(dynamic migrationBuilder)
                {
                    migrationBuilder.DropTable("Orders");
                    migrationBuilder.DropColumn("LegacyId", "Orders");
                    migrationBuilder.Sql("delete from Orders");
                }

                protected void Down(dynamic migrationBuilder)
                {
                }
            }
            """);

        Assert.Contains(issues, issue =>
            issue.RuleId == "EF004"
            && issue.DetectionMethod == IssueEnrichmentService.RoslynSyntaxAnalysis);
        Assert.Contains(issues, issue =>
            issue.RuleId == "EF005"
            && issue.DetectionMethod == IssueEnrichmentService.RoslynSyntaxAnalysis);
    }

    [Fact]
    public void IssueEnrichment_DoesNotInferSemanticDetectionFromLineNumber()
    {
        var service = new IssueEnrichmentService();
        var syntaxFinding = CreateIssue(
            "EF004-001",
            "EF004",
            "Migration contains destructive schema operation",
            "Migration calls migrationBuilder.DropTable.",
            IssueSeverity.Warning,
            AnalysisCategories.EfCore,
            "Store.Infrastructure",
            "Migrations/DropLegacy.cs",
            7) with
        {
            DetectionMethod = null
        };
        var absenceFinding = CreateIssue(
            "API005-001",
            "API005",
            "Global exception handling is missing",
            "Program.cs does not call UseExceptionHandler.",
            IssueSeverity.Error,
            AnalysisCategories.ApiReadiness,
            "Store.Api",
            "Program.cs",
            1) with
        {
            DetectionMethod = null
        };

        var enriched = service.Enrich([syntaxFinding, absenceFinding]);

        Assert.Equal(IssueEnrichmentService.RoslynSyntaxAnalysis, enriched.Single(issue => issue.RuleId == "EF004").DetectionMethod);
        Assert.Equal(IssueEnrichmentService.RoslynSyntaxAnalysis, enriched.Single(issue => issue.RuleId == "API005").DetectionMethod);
    }

    [Fact]
    public void EngineeringAssessment_FiltersCleanAndLowConfidenceRisksFromHighestRisks()
    {
        var service = new EngineeringAssessmentService();
        var lowConfidenceSecurity = CreateIssue(
            "SECLOW",
            "SECLOW",
            "Possible configuration concern",
            "A heuristic configuration signal was found.",
            IssueSeverity.Critical,
            AnalysisCategories.Security,
            "Store.Api",
            "appsettings.json",
            2) with
        {
            Confidence = IssueConfidence.Low
        };
        var highConfidenceDi = CreateIssue(
            "DI003",
            "DI003",
            "Singleton service captures scoped dependency",
            "A singleton service injects a scoped dependency.",
            IssueSeverity.Warning,
            AnalysisCategories.DependencyInjection,
            "Store.Api",
            "Program.cs",
            12) with
        {
            Confidence = IssueConfidence.High
        };

        var assessment = service.Build(
            92,
            new CategoryScores
            {
                Architecture = 100,
                Security = 99,
                EfCore = 100,
                DependencyInjection = 92,
                ApiReadiness = 100
            },
            [lowConfidenceSecurity, highConfidenceDi],
            CreateEmptyArchitectureMap());

        Assert.DoesNotContain(assessment.HighestRisks, risk => risk.Contains("Architecture", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(assessment.HighestRisks, risk => risk.Contains("Dependency injection", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(assessment.RecommendedPriorities, priority => priority.Contains("SECLOW", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalysisHistoryStore_ScopesReportsAndSanitizesSnapshots()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-history-{Guid.NewGuid():N}");
        var storePath = Path.Combine(tempRoot, "history.json");
        var repositoryRoot = Path.Combine(tempRoot, "repo");
        var result = CreateHistoryAnalysisResult(repositoryRoot);

        try
        {
            var store = new AnalysisHistoryStore(storePath);
            var userRun = await store.SaveAsync("user-a", result, AnalysisSourceTypes.SampleProject, "Sample Project");
            await store.SaveAsync("user-b", result with { SolutionName = "Other" }, AnalysisSourceTypes.ZipUpload, "other.zip");

            var userHistory = await store.ListAsync("user-a");
            Assert.Single(userHistory);
            Assert.Equal(userRun.Id, userHistory[0].Id);
            Assert.True(userHistory[0].CanRerun);

            Assert.Null(await store.GetAsync("user-b", userRun.Id));

            var detail = await store.GetAsync("user-a", userRun.Id);
            Assert.NotNull(detail);
            Assert.Null(detail.Result.SolutionPath);
            Assert.Null(detail.Result.RepositoryRoot);
            Assert.Null(detail.Result.SuppressionFilePath);
            Assert.True(detail.Result.IsHistoricalSnapshot);
            Assert.False(detail.Result.SourcePreviewAvailable);
            Assert.Equal(AnalysisResultSanitizer.HistoricalSourcePreviewUnavailableReason, detail.Result.SourcePreviewUnavailableReason);
            Assert.Empty(detail.Result.SourceFiles);

            var issue = Assert.Single(detail.Result.Issues);
            Assert.Equal("src/Api/Program.cs", issue.FilePath);
            Assert.Equal("src/Api/Program.cs", Assert.Single(issue.Evidence!).FilePath);
            Assert.Equal("src/Api/Api.csproj", Assert.Single(detail.Result.ProjectGraph.Projects).FilePath);
            Assert.Equal("src/Api/Api.csproj", Assert.Single(detail.Result.ArchitectureMap!.Projects).FilePath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AnalysisHistoryStore_DeleteIsScopedToUser()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-history-{Guid.NewGuid():N}");
        var storePath = Path.Combine(tempRoot, "history.json");

        try
        {
            var store = new AnalysisHistoryStore(storePath);
            var run = await store.SaveAsync("user-a", CreateHistoryAnalysisResult(Path.Combine(tempRoot, "repo")), AnalysisSourceTypes.ZipUpload, "upload.zip");

            Assert.False(await store.DeleteAsync("user-b", run.Id));
            Assert.NotNull(await store.GetAsync("user-a", run.Id));

            Assert.True(await store.DeleteAsync("user-a", run.Id));
            Assert.Null(await store.GetAsync("user-a", run.Id));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SuppressionsController_PostRejectsArbitrarySolutionPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-suppression-controller-{Guid.NewGuid():N}");
        var store = new AnalysisHistoryStore(Path.Combine(tempRoot, "history.json"));
        var controller = CreateSuppressionsController(store, "user-a");
        var solutionPath = Path.Combine(tempRoot, "External", "Sample.slnx");

        try
        {
            var response = await controller.Create(
                new CreateSuppressionRequest
                {
                    SolutionPath = solutionPath,
                    RuleId = "SEC001",
                    Status = "Accepted Risk",
                    Reason = "Legacy path should not authorize suppression writes."
                },
                CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
            var message = Assert.IsType<string>(badRequest.Value);
            Assert.DoesNotContain(tempRoot, message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(solutionPath)!, SuppressionService.SuppressionFileName)));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SuppressionsController_DeleteRejectsArbitrarySolutionPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-suppression-delete-{Guid.NewGuid():N}");
        var store = new AnalysisHistoryStore(Path.Combine(tempRoot, "history.json"));
        var controller = CreateSuppressionsController(store, "user-a");
        var solutionPath = Path.Combine(tempRoot, "External", "Sample.slnx");

        try
        {
            var response = await controller.Delete("sup_legacy", null, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(response);
            var message = Assert.IsType<string>(badRequest.Value);
            Assert.DoesNotContain(tempRoot, message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(solutionPath)!, SuppressionService.SuppressionFileName)));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AnalysisHistoryStore_SuppressionMutationIsScopedToUserAndDoesNotWriteRepositoryFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-history-suppression-{Guid.NewGuid():N}");
        var repositoryRoot = Path.Combine(tempRoot, "private-repo");
        var storePath = Path.Combine(tempRoot, "history.json");

        try
        {
            var store = new AnalysisHistoryStore(storePath);
            var run = await store.SaveAsync(
                "user-a",
                CreateHistoryAnalysisResult(repositoryRoot),
                AnalysisSourceTypes.GitHubRepo,
                "owner/private-repo",
                "https://github.com/owner/private-repo",
                "owner",
                "private-repo",
                "main",
                "Private");

            var denied = await store.CreateSuppressionAsync(
                "user-b",
                run.Id,
                "SEC001",
                "Wrong user.",
                "Accepted Risk",
                null);

            Assert.Null(denied);

            var suppression = await store.CreateSuppressionAsync(
                "user-a",
                run.Id,
                "SEC001",
                "Accepted during review.",
                "Accepted Risk",
                null);

            Assert.NotNull(suppression);
            Assert.False(File.Exists(Path.Combine(repositoryRoot, SuppressionService.SuppressionFileName)));

            var otherUserDelete = await store.RemoveSuppressionAsync("user-b", run.Id, suppression.Id);
            Assert.False(otherUserDelete);

            var detail = await store.GetAsync("user-a", run.Id);
            Assert.NotNull(detail);
            Assert.Contains(detail.Result.Issues, issue =>
                issue.Id == "SEC001"
                && issue.Suppression?.Id == suppression.Id
                && issue.Suppression.Status == "Accepted Risk");
            Assert.Equal(100, detail.Result.OverallScore);
            Assert.Equal(100, detail.Result.CategoryScores.Security);
            Assert.DoesNotContain(detail.Result.EngineeringAssessment!.RecommendedPriorities, priority =>
                priority.Contains("SEC001", StringComparison.OrdinalIgnoreCase));

            Assert.True(await store.RemoveSuppressionAsync("user-a", run.Id, suppression.Id));
            detail = await store.GetAsync("user-a", run.Id);
            Assert.NotNull(detail);
            Assert.DoesNotContain(detail.Result.Issues, issue => issue.Suppression?.Id == suppression.Id);
            Assert.True(detail.Result.OverallScore < 100);
            Assert.True(detail.Result.CategoryScores.Security < 100);
            Assert.Contains(detail.Result.EngineeringAssessment!.RecommendedPriorities, priority =>
                priority.Contains("SEC001", StringComparison.OrdinalIgnoreCase));
            Assert.False(File.Exists(Path.Combine(repositoryRoot, SuppressionService.SuppressionFileName)));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SuppressionsController_MutationRequiresAuthenticatedUser()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-suppression-auth-{Guid.NewGuid():N}");
        var store = new AnalysisHistoryStore(Path.Combine(tempRoot, "history.json"));
        var controller = CreateSuppressionsController(store, null);

        try
        {
            var response = await controller.Create(
                new CreateSuppressionRequest
                {
                    AnalysisRunId = "run-1",
                    FindingId = "SEC001",
                    Status = "Ignore",
                    Reason = "Unauthenticated request."
                },
                CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(response.Result);
            Assert.IsType<UnauthorizedResult>(await controller.Delete("sup_1", "run-1", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("https://github.com/dotnet/aspnetcore", "dotnet", "aspnetcore")]
    [InlineData("http://github.com/dotnet/runtime", "dotnet", "runtime")]
    [InlineData("github.com/cezarpedroso/dotdet", "cezarpedroso", "dotdet")]
    [InlineData("owner/repo.name", "owner", "repo.name")]
    public void GitHubRepositoryService_NormalizesSupportedRepositoryInputs(
        string input,
        string expectedOwner,
        string expectedName)
    {
        var repository = GitHubRepositoryService.NormalizeRepositoryInput(input);

        Assert.Equal(expectedOwner, repository.Owner);
        Assert.Equal(expectedName, repository.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/owner/repo")]
    [InlineData("owner")]
    [InlineData("owner/repo/extra")]
    [InlineData("-owner/repo")]
    [InlineData("owner/repo with spaces")]
    public void GitHubRepositoryService_RejectsInvalidRepositoryInputs(string input)
    {
        Assert.Throws<ArgumentException>(() => GitHubRepositoryService.NormalizeRepositoryInput(input));
    }

    [Fact]
    public async Task GitHubRepositoryService_UsesDefaultBranchFromPublicMetadata()
    {
        var service = new GitHubRepositoryService(new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "name": "repo",
                      "full_name": "owner/repo",
                      "private": false,
                      "default_branch": "release/v1",
                      "html_url": "https://github.com/owner/repo",
                      "description": "Sample",
                      "updated_at": "2026-07-10T12:00:00Z",
                      "owner": { "login": "owner" }
                    }
                    """)
            })));

        var metadata = await service.GetPublicRepositoryMetadataAsync(new GitHubRepositoryReference("owner", "repo"), CancellationToken.None);

        Assert.Equal("release/v1", metadata.DefaultBranch);
        Assert.Equal("owner/repo", metadata.FullName);
        Assert.Equal("https://github.com/owner/repo", metadata.HtmlUrl);
    }

    [Fact]
    public async Task GitHubRepositoryService_RejectsPrivateRepositoryMetadata()
    {
        var service = new GitHubRepositoryService(new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "name": "repo",
                      "full_name": "owner/repo",
                      "private": true,
                      "default_branch": "main",
                      "owner": { "login": "owner" }
                    }
                    """)
            })));

        var exception = await Assert.ThrowsAsync<GitHubRepositoryException>(() =>
            service.GetPublicRepositoryMetadataAsync(new GitHubRepositoryReference("owner", "repo"), CancellationToken.None));

        Assert.Equal("Repository not found or not public.", exception.Message);
    }

    [Fact]
    public async Task GitHubRepositoryService_AuthenticatedListingIncludesPrivateRepositories()
    {
        var service = new GitHubRepositoryService(new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("repo-token", request.Headers.Authorization?.Parameter);
            Assert.EndsWith("/user/repos?visibility=all&affiliation=owner,collaborator,organization_member&sort=updated&per_page=100&page=1", request.RequestUri?.ToString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    [
                      {
                        "name": "private-api",
                        "full_name": "owner/private-api",
                        "private": true,
                        "default_branch": "main",
                        "html_url": "https://github.com/owner/private-api",
                        "description": "ASP.NET Core private API",
                        "updated_at": "2026-07-10T12:00:00Z",
                        "owner": { "login": "owner" }
                      },
                      {
                        "name": "public-lib",
                        "full_name": "owner/public-lib",
                        "private": false,
                        "default_branch": "main",
                        "html_url": "https://github.com/owner/public-lib",
                        "description": "Library",
                        "updated_at": "2026-07-09T12:00:00Z",
                        "owner": { "login": "owner" }
                      }
                    ]
                    """)
            };
        })));

        var repositories = await service.GetRepositoriesForAuthenticatedUserAsync("repo-token", CancellationToken.None);

        Assert.Contains(repositories, repository =>
            repository.Owner == "owner"
            && repository.Name == "private-api"
            && repository.Visibility == "Private");
        Assert.Contains(repositories, repository =>
            repository.Owner == "owner"
            && repository.Name == "public-lib"
            && repository.Visibility == "Public");
    }

    [Fact]
    public async Task GitHubRepositoryService_AllowsPrivateRepositoryMetadataWithToken()
    {
        var service = new GitHubRepositoryService(new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("repo-token", request.Headers.Authorization?.Parameter);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "name": "private-api",
                      "full_name": "owner/private-api",
                      "private": true,
                      "default_branch": "trunk",
                      "html_url": "https://github.com/owner/private-api",
                      "owner": { "login": "owner" }
                    }
                    """)
            };
        })));

        var metadata = await service.GetRepositoryMetadataAsync(
            new GitHubRepositoryReference("owner", "private-api"),
            "repo-token",
            allowPrivate: true,
            CancellationToken.None);

        Assert.True(metadata.IsPrivate);
        Assert.Equal("trunk", metadata.DefaultBranch);
        Assert.Equal("owner/private-api", metadata.FullName);
    }

    [Fact]
    public async Task GitHubRepositoryAccessStore_ProtectsRepositoryTokenAtRest()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-github-access-{Guid.NewGuid():N}");
        var storePath = Path.Combine(tempRoot, "repository-access.json");
        var keyPath = Path.Combine(tempRoot, "keys");

        try
        {
            var provider = DataProtectionProvider.Create(new DirectoryInfo(keyPath));
            var store = new GitHubRepositoryAccessStore(storePath, provider);

            await store.SaveAsync("github-user-1", "gho_private_repo_token");

            var storedJson = await File.ReadAllTextAsync(storePath);
            Assert.DoesNotContain("gho_private_repo_token", storedJson, StringComparison.Ordinal);

            var status = await store.GetStatusAsync("github-user-1");
            Assert.True(status.IsEnabled);
            Assert.Equal("gho_private_repo_token", await store.GetAccessTokenAsync("github-user-1"));

            Assert.True(await store.DeleteAsync("github-user-1"));
            Assert.False((await store.GetStatusAsync("github-user-1")).IsEnabled);
            Assert.Null(await store.GetAccessTokenAsync("github-user-1"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AnalysisHistoryStore_SavesGitHubRepositoryMetadataWithoutSource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-history-{Guid.NewGuid():N}");
        var storePath = Path.Combine(tempRoot, "history.json");

        try
        {
            var store = new AnalysisHistoryStore(storePath);
            var result = CreateHistoryAnalysisResult(Path.Combine(tempRoot, "repo"));
            var run = await store.SaveAsync(
                "user-a",
                result,
                AnalysisSourceTypes.GitHubRepo,
                "owner/repo",
                "https://github.com/owner/repo",
                "owner",
                "repo",
                "release/v1");

            var summary = Assert.Single(await store.ListAsync("user-a"));
            Assert.Equal(run.Id, summary.Id);
            Assert.Equal(AnalysisSourceTypes.GitHubRepo, summary.SourceType);
            Assert.Equal("owner/repo", summary.SourceLabel);
            Assert.Equal("owner", summary.GitHubOwner);
            Assert.Equal("repo", summary.GitHubRepo);
            Assert.Equal("release/v1", summary.GitRef);
            Assert.False(summary.CanRerun);

            var detail = await store.GetAsync("user-a", run.Id);
            Assert.NotNull(detail);
            Assert.True(detail.Result.IsHistoricalSnapshot);
            Assert.False(detail.Result.SourcePreviewAvailable);
            Assert.Equal(AnalysisResultSanitizer.HistoricalSourcePreviewUnavailableReason, detail.Result.SourcePreviewUnavailableReason);
            Assert.Empty(detail.Result.SourceFiles);
            Assert.Null(detail.Result.RepositoryRoot);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void AnalysisResultSanitizer_LiveRepositoryResponseKeepsSourcePreviewWithoutTempPaths()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), $"dotdet-live-repo-{Guid.NewGuid():N}");
        var result = CreateHistoryAnalysisResult(repositoryRoot);

        var liveResult = AnalysisResultSanitizer.CreateLiveRepositoryResponse(result);

        Assert.False(liveResult.IsHistoricalSnapshot);
        Assert.True(liveResult.SourcePreviewAvailable);
        Assert.Null(liveResult.SourcePreviewUnavailableReason);
        Assert.Null(liveResult.SolutionPath);
        Assert.Null(liveResult.RepositoryRoot);
        Assert.Null(liveResult.SuppressionFilePath);

        var sourceFile = Assert.Single(liveResult.SourceFiles);
        Assert.Equal("src/Api/Program.cs", sourceFile.FilePath);
        Assert.Equal("src/Api/Program.cs", sourceFile.RelativePath);
        Assert.Contains("WebApplication.CreateBuilder", sourceFile.Content);

        var issue = Assert.Single(liveResult.Issues);
        Assert.Equal("src/Api/Program.cs", issue.FilePath);
        Assert.Equal("src/Api/Program.cs", Assert.Single(issue.Evidence!).FilePath);
        Assert.Equal("src/Api/Api.csproj", Assert.Single(liveResult.ProjectGraph.Projects).FilePath);
        Assert.Equal("src/Api/Api.csproj", Assert.Single(liveResult.ArchitectureMap!.Projects).FilePath);

        var serialized = System.Text.Json.JsonSerializer.Serialize(liveResult);
        Assert.DoesNotContain(repositoryRoot.Replace('\\', '/'), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repositoryRoot, serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArchitectureMap_DoesNotFlagPresentationToInfrastructureWithoutRelatedFinding()
    {
        var context = CreateArchitectureContext();
        var graph = new ProjectGraph
        {
            Projects =
            [
                new ProjectNode { Name = "Store.Web", FilePath = "Store.Web.csproj", LogicalLayer = AnalyzerUtilities.PresentationLayer, IsAspNetCoreEntryPoint = true },
                new ProjectNode { Name = "Store.Infrastructure", FilePath = "Store.Infrastructure.csproj", LogicalLayer = AnalyzerUtilities.InfrastructureLayer }
            ],
            Dependencies =
            [
                new ProjectDependency { SourceProjectName = "Store.Web", TargetProjectName = "Store.Infrastructure" }
            ]
        };

        var map = new ArchitectureMapService().Build(context, graph, []);
        var dependency = Assert.Single(map.Dependencies);

        Assert.False(dependency.IsViolation);
        Assert.Null(dependency.RuleId);
        Assert.Null(dependency.RelatedFindingId);
        Assert.Empty(map.Violations);
    }

    [Fact]
    public void ArchitectureMap_FlagsPresentationToInfrastructureWhenRelatedEvidenceExists()
    {
        var context = CreateArchitectureContext();
        var graph = new ProjectGraph
        {
            Projects =
            [
                new ProjectNode { Name = "Store.Web", FilePath = "Store.Web.csproj", LogicalLayer = AnalyzerUtilities.PresentationLayer, IsAspNetCoreEntryPoint = true },
                new ProjectNode { Name = "Store.Infrastructure", FilePath = "Store.Infrastructure.csproj", LogicalLayer = AnalyzerUtilities.InfrastructureLayer }
            ],
            Dependencies =
            [
                new ProjectDependency { SourceProjectName = "Store.Web", TargetProjectName = "Store.Infrastructure" }
            ]
        };
        var directMisuseFinding = CreateIssue(
            "ARCH-PRESENTATION-INFRA",
            "ARCH-PRESENTATION-INFRA",
            "Presentation uses Infrastructure outside composition root",
            "Store.Web controller references Store.Infrastructure persistence type directly.",
            IssueSeverity.Warning,
            AnalysisCategories.Architecture,
            "Store.Web",
            "Controllers/CatalogController.cs",
            14);

        var map = new ArchitectureMapService().Build(context, graph, [directMisuseFinding]);
        var dependency = Assert.Single(map.Dependencies);

        Assert.True(dependency.IsViolation);
        Assert.Equal("ARCHMAP005", dependency.RuleId);
        Assert.Equal(directMisuseFinding.Id, dependency.RelatedFindingId);
        Assert.Single(map.Violations);
    }

    [Fact]
    public void ArchitectureMap_TreatsInfrastructureToApplicationAsValid()
    {
        var applicationProject = CreateAnalyzedProject("Store.Application", AnalyzerUtilities.ApplicationLayer);
        var infrastructureProject = CreateAnalyzedProject("Store.Infrastructure", AnalyzerUtilities.InfrastructureLayer);
        var context = new SolutionAnalysisContext
        {
            SolutionPath = "Store.slnx",
            SolutionName = "Store",
            RootDirectory = Directory.GetCurrentDirectory(),
            Projects = [applicationProject, infrastructureProject],
            SourceFiles = [],
            SemanticProjects = [],
            SemanticDocuments = [],
            AppSettingsFiles = []
        };
        var graph = new ProjectGraph
        {
            Projects =
            [
                new ProjectNode { Name = "Store.Application", FilePath = "Store.Application.csproj", LogicalLayer = AnalyzerUtilities.ApplicationLayer },
                new ProjectNode { Name = "Store.Infrastructure", FilePath = "Store.Infrastructure.csproj", LogicalLayer = AnalyzerUtilities.InfrastructureLayer }
            ],
            Dependencies =
            [
                new ProjectDependency { SourceProjectName = "Store.Infrastructure", TargetProjectName = "Store.Application" }
            ]
        };

        var map = new ArchitectureMapService().Build(context, graph, []);
        var dependency = Assert.Single(map.Dependencies);

        Assert.False(dependency.IsViolation);
        Assert.Null(dependency.RuleId);
        Assert.Empty(map.Violations);
    }

    [Fact]
    public void ArchitectureAnalyzer_ReportsSameCycleOnce()
    {
        var projectAPath = AnalyzerUtilities.NormalizePath(Path.Combine(Directory.GetCurrentDirectory(), "Store.A.csproj"));
        var projectBPath = AnalyzerUtilities.NormalizePath(Path.Combine(Directory.GetCurrentDirectory(), "Store.B.csproj"));
        var projectA = CreateAnalyzedProject("Store.A", AnalyzerUtilities.ApplicationLayer) with
        {
            FilePath = projectAPath,
            ProjectReferencePaths = [projectBPath]
        };
        var projectB = CreateAnalyzedProject("Store.B", AnalyzerUtilities.ApplicationLayer) with
        {
            FilePath = projectBPath,
            ProjectReferencePaths = [projectAPath]
        };
        var context = new SolutionAnalysisContext
        {
            SolutionPath = "Store.slnx",
            SolutionName = "Store",
            RootDirectory = Directory.GetCurrentDirectory(),
            Projects = [projectA, projectB],
            SourceFiles = [],
            SemanticProjects = [],
            SemanticDocuments = [],
            AppSettingsFiles = []
        };

        var issues = new ArchitectureAnalyzer(new SemanticAnalysisHelper()).Analyze(context);

        Assert.Single(issues, issue => issue.RuleId == "ARCH005");
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
                builder.Services.AddScoped<IDuplicateCatalogService, DuplicateCatalogService>();
                builder.Services.AddScoped<IDuplicateCatalogService, DuplicateCatalogService>();
                var app = builder.Build();
                app.MapControllers();
                app.Run();
                """);
            WriteFile(Path.Combine(tempRoot, "src", "DiApplicability.Api", "Services.cs"), """
                public interface IInternalDependency { }
                public interface IRegisteredDependency { }
                public interface IControllerDependency { }
                public interface IDuplicateCatalogService { }

                public sealed class DuplicateCatalogService : IDuplicateCatalogService { }

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

                public sealed class CheckoutController : ControllerBase
                {
                    public CheckoutController(IControllerDependency dependency) { }
                }
                """);

            var result = await CreateService().AnalyzeAsync(solutionPath, CancellationToken.None);
            var duplicateRegistration = Assert.Single(result.Issues, issue =>
                issue.RuleId == "DI001"
                && issue.Title == "Duplicate registrations for IDuplicateCatalogService");
            Assert.Equal("DI001|DiApplicability.Api|IDuplicateCatalogService", duplicateRegistration.RootCauseKey);
            Assert.Equal(2, duplicateRegistration.Evidence?.Count);
            Assert.Contains(duplicateRegistration.Evidence!, evidence =>
                evidence.Detail.Contains("Scoped", StringComparison.Ordinal)
                && evidence.LineNumber is > 0);
            Assert.DoesNotContain(result.Issues, issue =>
                issue.RuleId == "DI001"
                && issue.Title == "Duplicate dependency injection registration");

            Assert.DoesNotContain(result.Issues, issue =>
                issue.RuleId == "DI002"
                && issue.Description.Contains("IInternalDependency", StringComparison.Ordinal));
            Assert.Contains(result.Issues, issue =>
                issue.RuleId == "DI002"
                && issue.Description.Contains("IRegisteredDependency", StringComparison.Ordinal)
                && issue.WhyDetected?.Contains("not found in the project service registrations", StringComparison.Ordinal) == true);
            var controllerDependency = Assert.Single(result.Issues, issue =>
                issue.RuleId == "DI002"
                && issue.Title == "IControllerDependency appears unregistered");
            Assert.Equal(2, controllerDependency.Evidence?.Count);
            Assert.Contains(controllerDependency.Evidence!, evidence =>
                evidence.Detail.Contains("SampleController injects IControllerDependency", StringComparison.Ordinal));
            Assert.Contains(controllerDependency.Evidence!, evidence =>
                evidence.Detail.Contains("CheckoutController injects IControllerDependency", StringComparison.Ordinal));
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
            && issue.RuleId == "DI001"
            && issue.Title.StartsWith("Duplicate registrations for ", StringComparison.Ordinal)
            && issue.FilePath?.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase) == true
            && issue.LineNumber is > 0
            && issue.Evidence is { Count: >= 2 });

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
            && issue.Title == "IPaymentGateway appears unregistered"
            && issue.Description.Contains("IPaymentGateway", StringComparison.Ordinal)
            && issue.LineNumber is > 0
            && issue.Evidence is { Count: >= 1 });

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

    [Fact]
    public async Task AnalyzeAsync_SourcePreviewIncludesRepositoryTreeBeyondFindingFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-source-preview-{Guid.NewGuid():N}");
        var solutionPath = Path.Combine(tempRoot, "SchemaArchitect.slnx");
        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteFile(solutionPath, """
                <Solution>
                  <Project Path="src/SchemaArchitect.Core/SchemaArchitect.Core.csproj" />
                  <Project Path="src/SchemaArchitect.Web/SchemaArchitect.Web.csproj" />
                  <Project Path="tests/SchemaArchitect.Tests/SchemaArchitect.Tests.csproj" />
                </Solution>
                """);
            WriteFile(Path.Combine(tempRoot, "SchemaArchitect.sln"), """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                """);
            WriteFile(Path.Combine(tempRoot, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "Directory.Packages.props"), """
                <Project>
                  <ItemGroup>
                    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
                  </ItemGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "README.md"), "# SchemaArchitect");
            WriteFile(Path.Combine(tempRoot, "package-lock.json"), "{}");
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Core", "SchemaArchitect.Core.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Core", "DomainModel.cs"), """
                namespace SchemaArchitect.Core;

                public sealed class DomainModel
                {
                    public string Name { get; init; } = "";
                }
                """);
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Web", "SchemaArchitect.Web.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="..\SchemaArchitect.Core\SchemaArchitect.Core.csproj" />
                  </ItemGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Web", "Program.cs"), """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllers();
                var app = builder.Build();
                app.MapControllers();
                app.Run();
                """);
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Web", "appsettings.Development.json"), """
                {
                  "ConnectionStrings": {
                    "Default": "Server=localhost;Database=SchemaArchitect"
                  }
                }
                """);
            WriteFile(Path.Combine(tempRoot, "tests", "SchemaArchitect.Tests", "SchemaArchitect.Tests.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
                    <ProjectReference Include="..\..\src\SchemaArchitect.Core\SchemaArchitect.Core.csproj" />
                  </ItemGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "tests", "SchemaArchitect.Tests", "DomainModelTests.cs"), """
                namespace SchemaArchitect.Tests;

                public sealed class DomainModelTests
                {
                    public void CanCreateModel() { }
                }
                """);
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Core", "bin", "Debug", "net9.0", "Generated.cs"), "public sealed class Generated { }");
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Web", "obj", "Debug", "net9.0", "Temporary.cs"), "public sealed class Temporary { }");
            WriteFile(Path.Combine(tempRoot, "node_modules", "leftpad", "Index.cs"), "public sealed class NodeModuleFile { }");
            WriteFile(Path.Combine(tempRoot, ".git", "hooks", "Hook.cs"), "public sealed class GitHook { }");

            var result = await CreateService().AnalyzeAsync(solutionPath, CancellationToken.None);
            var liveResult = AnalysisResultSanitizer.CreateLiveRepositoryResponse(result);

            Assert.Contains(liveResult.ProjectGraph.Projects, project => project.Name == "SchemaArchitect.Core");
            Assert.Contains(liveResult.ProjectGraph.Projects, project => project.Name == "SchemaArchitect.Web");
            Assert.Contains(liveResult.ProjectGraph.Projects, project => project.Name == "SchemaArchitect.Tests" && project.IsTestProject);

            AssertSourcePreviewContains(liveResult, "SchemaArchitect.sln");
            AssertSourcePreviewContains(liveResult, "SchemaArchitect.slnx");
            AssertSourcePreviewContains(liveResult, "Directory.Build.props");
            AssertSourcePreviewContains(liveResult, "Directory.Packages.props");
            AssertSourcePreviewContains(liveResult, "README.md");
            AssertSourcePreviewContains(liveResult, "src/SchemaArchitect.Core/SchemaArchitect.Core.csproj");
            AssertSourcePreviewContains(liveResult, "src/SchemaArchitect.Core/DomainModel.cs");
            AssertSourcePreviewContains(liveResult, "src/SchemaArchitect.Web/SchemaArchitect.Web.csproj");
            AssertSourcePreviewContains(liveResult, "src/SchemaArchitect.Web/Program.cs");
            AssertSourcePreviewContains(liveResult, "src/SchemaArchitect.Web/appsettings.Development.json");
            AssertSourcePreviewContains(liveResult, "tests/SchemaArchitect.Tests/SchemaArchitect.Tests.csproj");
            AssertSourcePreviewContains(liveResult, "tests/SchemaArchitect.Tests/DomainModelTests.cs");

            var findingPaths = liveResult.Issues
                .Select(issue => issue.FilePath)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("src/SchemaArchitect.Core/DomainModel.cs", findingPaths);
            Assert.Contains(liveResult.SourceFiles, file => file.RelativePath == "src/SchemaArchitect.Core/DomainModel.cs");

            Assert.DoesNotContain(liveResult.SourceFiles, file => file.RelativePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(liveResult.SourceFiles, file => file.RelativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(liveResult.SourceFiles, file => file.RelativePath.StartsWith("node_modules/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(liveResult.SourceFiles, file => file.RelativePath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(liveResult.SourceFiles, file => file.RelativePath == "package-lock.json");
            Assert.All(liveResult.SourceFiles, file =>
            {
                Assert.False(Path.IsPathRooted(file.FilePath));
                Assert.DoesNotContain(tempRoot, file.FilePath, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(tempRoot, file.RelativePath, StringComparison.OrdinalIgnoreCase);
            });
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void AssertSourcePreviewContains(AnalysisResult result, string relativePath)
    {
        Assert.Contains(result.SourceFiles, file =>
            file.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)
            && file.FilePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(file.Content));
    }

    private static IReadOnlyList<AnalysisIssue> AnalyzeMigrationSource(string source)
    {
        var project = CreateAnalyzedProject("Store.Infrastructure", AnalyzerUtilities.InfrastructureLayer);
        var tree = CSharpSyntaxTree.ParseText(source);
        var context = new SolutionAnalysisContext
        {
            SolutionPath = "Store.slnx",
            SolutionName = "Store",
            RootDirectory = Directory.GetCurrentDirectory(),
            Projects = [project],
            SourceFiles =
            [
                new SourceFileContext
                {
                    Project = project,
                    FilePath = Path.Combine("Migrations", "202607130001_TestMigration.cs"),
                    Text = source,
                    Root = tree.GetCompilationUnitRoot()
                }
            ],
            SemanticProjects = [],
            SemanticDocuments = [],
            AppSettingsFiles = []
        };

        return new EfCoreAnalyzer(new SemanticAnalysisHelper()).Analyze(context);
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
            new FindingGroupingService(),
            new IssueEnrichmentService(),
            new ScoringService(),
            new ArchitectureMapService(),
            new EngineeringAssessmentService(),
            new SuppressionService(),
            NullLogger<SolutionAnalysisService>.Instance);
    }

    private static SuppressionsController CreateSuppressionsController(
        AnalysisHistoryStore store,
        string? userId)
    {
        var controller = new SuppressionsController(store)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = string.IsNullOrWhiteSpace(userId)
                        ? new ClaimsPrincipal(new ClaimsIdentity())
                        : new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, userId)],
                            "TestAuth"))
                }
            }
        };

        return controller;
    }

    private static AnalysisResult CreateHistoryAnalysisResult(string repositoryRoot)
    {
        var projectPath = Path.Combine(repositoryRoot, "src", "Api", "Api.csproj");
        var sourcePath = Path.Combine(repositoryRoot, "src", "Api", "Program.cs");

        return new AnalysisResult
        {
            SolutionName = "HistorySample",
            AnalyzedAt = DateTimeOffset.UtcNow,
            OverallScore = 82,
            CategoryScores = new CategoryScores
            {
                Architecture = 100,
                DependencyInjection = 90,
                EfCore = 85,
                Security = 70,
                ApiReadiness = 78
            },
            Issues =
            [
                new AnalysisIssue
                {
                    Id = "SEC001",
                    RuleId = "SEC001",
                    Title = "Configuration risk",
                    Description = "A configuration value requires review.",
                    Severity = IssueSeverity.Warning,
                    Category = AnalysisCategories.Security,
                    ProjectName = "Api",
                    FilePath = sourcePath,
                    LineNumber = 12,
                    Recommendation = "Move sensitive values into a managed secret store.",
                    Evidence =
                    [
                        new AnalysisEvidence
                        {
                            Label = "File",
                            Detail = "Program.cs contains the relevant configuration call.",
                            FilePath = sourcePath,
                            LineNumber = 12
                        }
                    ]
                }
            ],
            ProjectGraph = new ProjectGraph
            {
                Projects =
                [
                    new ProjectNode
                    {
                        Name = "Api",
                        FilePath = projectPath,
                        LogicalLayer = AnalyzerUtilities.PresentationLayer,
                        IsAspNetCoreEntryPoint = true
                    }
                ],
                Dependencies = []
            },
            SourceFiles =
            [
                new AnalysisSourceFile
                {
                    ProjectName = "Api",
                    FilePath = sourcePath,
                    RelativePath = "src/Api/Program.cs",
                    Content = "var builder = WebApplication.CreateBuilder(args);",
                    Language = "csharp"
                }
            ],
            ArchitectureMap = new ArchitectureMap
            {
                Layers =
                [
                    new ArchitectureLayer
                    {
                        Name = AnalyzerUtilities.PresentationLayer,
                        Order = 1,
                        ProjectNames = ["Api"]
                    }
                ],
                Projects =
                [
                    new ArchitectureMapProject
                    {
                        Name = "Api",
                        FilePath = projectPath,
                        Layer = AnalyzerUtilities.PresentationLayer,
                        NamespaceRoot = "Api",
                        IssueCount = 1,
                        CriticalOrErrorCount = 0
                    }
                ],
                Dependencies = [],
                Violations = []
            },
            SolutionPath = Path.Combine(repositoryRoot, "HistorySample.slnx"),
            RepositoryRoot = repositoryRoot,
            SuppressionFilePath = Path.Combine(repositoryRoot, "dotdet.suppressions.json")
        };
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

    private static AnalysisIssue CreateIssue(
        string id,
        string ruleId,
        string title,
        string description,
        IssueSeverity severity,
        string category,
        string projectName,
        string filePath,
        int? lineNumber)
    {
        return new AnalysisIssue
        {
            Id = id,
            RuleId = ruleId,
            Title = title,
            Description = description,
            Severity = severity,
            Category = category,
            ProjectName = projectName,
            FilePath = filePath,
            LineNumber = lineNumber,
            Recommendation = "Review and remediate the root cause.",
            Confidence = IssueConfidence.High,
            DetectionMethod = IssueEnrichmentService.RoslynSemanticAnalysis
        };
    }

    private static ArchitectureMap CreateEmptyArchitectureMap()
    {
        return new ArchitectureMap
        {
            Layers = [],
            Projects = [],
            Dependencies = [],
            Violations = []
        };
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private static SolutionAnalysisContext CreateArchitectureContext()
    {
        var webProject = CreateAnalyzedProject("Store.Web", AnalyzerUtilities.PresentationLayer, isWebProject: true, isAspNetCoreEntryPoint: true);
        var infrastructureProject = CreateAnalyzedProject("Store.Infrastructure", AnalyzerUtilities.InfrastructureLayer);

        return new SolutionAnalysisContext
        {
            SolutionPath = "Store.slnx",
            SolutionName = "Store",
            RootDirectory = Directory.GetCurrentDirectory(),
            Projects = [webProject, infrastructureProject],
            SourceFiles = [],
            SemanticProjects = [],
            SemanticDocuments = [],
            AppSettingsFiles = []
        };
    }

    private static AnalyzedProject CreateAnalyzedProject(
        string name,
        string logicalLayer,
        bool isWebProject = false,
        bool isAspNetCoreEntryPoint = false)
    {
        return new AnalyzedProject
        {
            Name = name,
            FilePath = $"{name}.csproj",
            AssemblyName = name,
            DirectoryPath = Directory.GetCurrentDirectory(),
            Sdk = isWebProject ? "Microsoft.NET.Sdk.Web" : "Microsoft.NET.Sdk",
            TargetFramework = "net9.0",
            IsWebProject = isWebProject,
            IsAspNetCoreEntryPoint = isAspNetCoreEntryPoint,
            IsTestProject = false,
            LogicalLayer = logicalLayer,
            LoadedWithMsBuild = false,
            PackageReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ProjectReferencePaths = [],
            TransitiveProjectReferencePaths = []
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

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
