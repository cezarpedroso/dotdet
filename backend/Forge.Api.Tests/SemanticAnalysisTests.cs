using Forge.Api.Analysis;
using Forge.Api.Analyzers;
using Forge.Api.Contracts;
using Forge.Api.Controllers;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    public void ScoringService_CapsConfirmedErrorsWithoutTreatingInferredErrorsAsBlockers()
    {
        var service = new ScoringService();
        var healthyScores = new CategoryScores
        {
            Architecture = 95,
            Security = 95,
            EfCore = 95,
            DependencyInjection = 95,
            ApiReadiness = 95
        };
        var confirmedError = CreateIssue("SEC-CONFIRMED", IssueSeverity.Error) with
        {
            Confidence = IssueConfidence.High,
            RootCauseKey = "SEC-CONFIRMED"
        };
        var inferredError = CreateIssue("SEC-INFERRED", IssueSeverity.Error) with
        {
            Confidence = IssueConfidence.Medium,
            RootCauseKey = "SEC-INFERRED"
        };

        Assert.Equal(88, service.CalculateOverallScore(healthyScores, [confirmedError]));
        Assert.Equal(95, service.CalculateOverallScore(healthyScores, [inferredError]));
    }

    [Fact]
    public void AnalysisEndpoints_ApplyAuthenticationRateAndConcurrencyPolicies()
    {
        var zipMethod = typeof(AnalysisController).GetMethod(nameof(AnalysisController.AnalyzeZip))!;
        var sampleMethod = typeof(AnalysisController).GetMethod(nameof(AnalysisController.AnalyzeSample))!;

        Assert.NotNull(zipMethod.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(zipMethod.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(zipMethod.GetCustomAttributes(typeof(AnalysisExecutionAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(sampleMethod.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(typeof(GitHubController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
    }

    [Fact]
    public void AnalysisConcurrencyGate_LimitsEachCallerIndependently()
    {
        var gate = new AnalysisConcurrencyGate(Options.Create(new AnalysisExecutionOptions
        {
            MaxConcurrentPerCaller = 1
        }));

        using var first = gate.TryAcquire("user:one");
        Assert.NotNull(first);
        Assert.Null(gate.TryAcquire("user:one"));
        using var otherUser = gate.TryAcquire("user:two");
        Assert.NotNull(otherUser);
    }

    [Fact]
    public void AnalysisFidelity_ReportsActualSemanticCoverage()
    {
        var options = AnalysisLoadOptions.For(AnalysisInputTrust.TrustedLocalDevelopment);

        var full = SolutionAnalysisService.DetermineAnalysisFidelity(options, 3, 3, 12);
        var degraded = SolutionAnalysisService.DetermineAnalysisFidelity(options, 3, 1, 4);
        var fallback = SolutionAnalysisService.DetermineAnalysisFidelity(options, 3, 0, 0);

        Assert.Equal("Roslyn Semantic Analysis", full.AnalysisFidelity);
        Assert.False(full.SemanticAnalysisSkipped);
        Assert.True(full.HasSemanticData);

        Assert.Equal("Project Load Degraded", degraded.AnalysisFidelity);
        Assert.True(degraded.SemanticAnalysisSkipped);
        Assert.True(degraded.HasSemanticData);
        Assert.Contains("1 of 3", degraded.SemanticAnalysisSkippedReason);

        Assert.Equal("Syntax Fallback", fallback.AnalysisFidelity);
        Assert.True(fallback.SemanticAnalysisSkipped);
        Assert.False(fallback.HasSemanticData);
        Assert.Contains("did not produce semantic projects and documents", fallback.SemanticAnalysisSkippedReason);
    }

    [Fact]
    public void AnalysisFidelity_UntrustedInputAlwaysReportsSafeSyntaxAnalysis()
    {
        var fidelity = SolutionAnalysisService.DetermineAnalysisFidelity(
            AnalysisLoadOptions.For(AnalysisInputTrust.UntrustedArchive),
            projectCount: 3,
            semanticProjectCount: 3,
            semanticDocumentCount: 12);

        Assert.Equal("Safe Syntax Analysis", fidelity.AnalysisFidelity);
        Assert.True(fidelity.SemanticAnalysisSkipped);
        Assert.False(fidelity.HasSemanticData);
        Assert.Contains("untrusted input", fidelity.SemanticAnalysisSkippedReason);
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
    public void ScoringService_IgnoresInfoOnlyFindingsForProductionReadiness()
    {
        var service = new ScoringService();
        var infoOnlyFindings = new[]
        {
            CreateIssue(
                "API004-INFO",
                "API004",
                "Health checks are missing",
                "A Web UI host does not map health checks.",
                IssueSeverity.Info,
                AnalysisCategories.ApiReadiness,
                "SchemaArchitect.Web",
                "Program.cs",
                1),
            CreateIssue(
                "API005-INFO",
                "API005",
                "Global exception handling is missing",
                "A Web UI host does not configure global exception handling.",
                IssueSeverity.Info,
                AnalysisCategories.ApiReadiness,
                "SchemaArchitect.Web",
                "Program.cs",
                1)
        };

        var categoryScores = service.CalculateCategoryScores(infoOnlyFindings);

        Assert.Equal(100, categoryScores.ApiReadiness);
        Assert.Equal(100, service.CalculateOverallScore(categoryScores, infoOnlyFindings));
    }

    [Fact]
    public void EngineeringAssessment_InfoOnlyFindingsDoNotCreateActiveRisks()
    {
        var service = new EngineeringAssessmentService();
        var infoOnlyFindings = new[]
        {
            CreateIssue(
                "API004-INFO",
                "API004",
                "Health checks are missing",
                "A Web UI host does not map health checks.",
                IssueSeverity.Info,
                AnalysisCategories.ApiReadiness,
                "SchemaArchitect.Web",
                "Program.cs",
                1)
        };
        var assessment = service.Build(
            100,
            new CategoryScores
            {
                Architecture = 100,
                Security = 100,
                EfCore = 100,
                DependencyInjection = 100,
                ApiReadiness = 100
            },
            infoOnlyFindings,
            CreateEmptyArchitectureMap());

        Assert.Contains("No active production root-cause findings were detected", assessment.ScoreExplanation);
        Assert.DoesNotContain("across 1 active root-cause", assessment.ScoreExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(assessment.HighestRisks, risk => risk.Contains("API", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(assessment.HighestRisks, risk => risk.Contains("No significant production risks", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(assessment.RecommendedPriorities, priority => priority.Contains("Review remaining warnings", StringComparison.OrdinalIgnoreCase));
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
            Assert.Equal("SEC001|Api|src/Api/Program.cs|Configuration risk", issue.RootCauseKey);
            Assert.Equal("src/Api/Program.cs", Assert.Single(issue.Evidence!).FilePath);
            Assert.Equal("src/Api/Api.csproj", Assert.Single(detail.Result.ProjectGraph.Projects).FilePath);
            Assert.Equal("src/Api/Api.csproj", Assert.Single(detail.Result.ArchitectureMap!.Projects).FilePath);
            AssertNoServerPathLeak(detail, repositoryRoot);
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
    public async Task AnalysisController_AnalyzePath_ReturnsNotFoundOutsideDevelopment()
    {
        var controller = new AnalysisController(
            CreateService(),
            new ZipExtractionService(),
            new AnalysisHistoryStore(Path.Combine(Path.GetTempPath(), $"dotdet-history-{Guid.NewGuid():N}.json")),
            new TestWebHostEnvironment { EnvironmentName = Environments.Production },
            NullLogger<AnalysisController>.Instance);

        var response = await controller.AnalyzePath(
            new AnalyzePathRequest(@"C:\server\private\Secret.sln"),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task AnalysisController_AnalyzeSample_ReturnsOnlyRepositoryRelativePaths()
    {
        var controller = new AnalysisController(
            CreateService(),
            new ZipExtractionService(),
            new AnalysisHistoryStore(Path.Combine(Path.GetTempPath(), $"dotdet-history-{Guid.NewGuid():N}.json")),
            new TestWebHostEnvironment { EnvironmentName = Environments.Development },
            NullLogger<AnalysisController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.AnalyzeSample(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<AnalysisResult>(ok.Value);

        Assert.Null(result.SolutionPath);
        Assert.Null(result.RepositoryRoot);
        Assert.Null(result.SuppressionFilePath);
        Assert.All(result.SourceFiles, file =>
        {
            Assert.False(Path.IsPathRooted(file.FilePath));
            Assert.False(Path.IsPathRooted(file.RelativePath));
        });
        Assert.All(result.Issues.Where(issue => !string.IsNullOrWhiteSpace(issue.FilePath)), issue =>
            Assert.False(Path.IsPathRooted(issue.FilePath!)));
        Assert.All(result.ProjectGraph.Projects, project => Assert.False(Path.IsPathRooted(project.FilePath)));
        Assert.All(result.ArchitectureMap?.Projects ?? [], project => Assert.False(Path.IsPathRooted(project.FilePath)));
        Assert.All(result.Issues, issue => AssertRootCauseKeyIsSafe(issue.RootCauseKey));
        AssertNoServerPathLeak(result, Path.GetDirectoryName(SolutionAnalysisService.ResolveSampleSolutionPath())!);
    }

    [Fact]
    public async Task AnalysisHistoryStore_ResanitizesLegacyRootCauseKeysOnRead()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-legacy-history-{Guid.NewGuid():N}");
        var storePath = Path.Combine(tempRoot, "history.json");
        var repositoryRoot = Path.Combine(tempRoot, "private-repo");
        var result = CreateHistoryAnalysisResult(repositoryRoot);
        var run = new AnalysisHistoryRun
        {
            Id = "legacy-run",
            UserId = "user-a",
            SolutionName = result.SolutionName,
            SourceType = AnalysisSourceTypes.GitHubRepo,
            SourceLabel = "owner/private-repo",
            Score = result.OverallScore,
            Grade = "B",
            Status = "Needs Review",
            OpenFindingCount = 1,
            TotalFindingCount = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            ReportSnapshot = result with
            {
                RepositoryRoot = null,
                Issues = result.Issues.Select(issue => issue with
                {
                    RootCauseKey = $"SEC001|Api|{Path.Combine(repositoryRoot, "src", "Api", "Program.cs")}|Configuration risk"
                }).ToArray()
            }
        };

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(storePath, JsonSerializer.Serialize(new[] { run }));

            var detail = await new AnalysisHistoryStore(storePath).GetAsync("user-a", run.Id);

            Assert.NotNull(detail);
            Assert.Contains("<unknown-file>", Assert.Single(detail.Result.Issues).RootCauseKey);
            AssertNoServerPathLeak(detail, repositoryRoot, tempRoot);
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
        var original = CreateHistoryAnalysisResult(repositoryRoot);
        var configuredIssue = Assert.Single(original.Issues) with
        {
            WhyDetected = $"DotDet inspected {Path.Combine(repositoryRoot, "src", "Api", "Program.cs")}",
            Evidence =
            [
                new AnalysisEvidence
                {
                    Label = "File",
                    Detail = $"Configuration was detected in {Path.Combine(repositoryRoot, "src", "Api", "Program.cs")}",
                    FilePath = Path.Combine(repositoryRoot, "src", "Api", "Program.cs"),
                    LineNumber = 12
                }
            ]
        };
        var result = original with { Issues = [configuredIssue] };

        var liveResult = AnalysisResultSanitizer.CreateLiveResponse(result);

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
        Assert.Equal("SEC001|Api|src/Api/Program.cs|Configuration risk", issue.RootCauseKey);
        Assert.Equal("src/Api/Program.cs", Assert.Single(issue.Evidence!).FilePath);
        Assert.DoesNotContain(repositoryRoot, issue.WhyDetected!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repositoryRoot, Assert.Single(issue.Evidence!).Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("src/Api/Api.csproj", Assert.Single(liveResult.ProjectGraph.Projects).FilePath);
        Assert.Equal("src/Api/Api.csproj", Assert.Single(liveResult.ArchitectureMap!.Projects).FilePath);

        var serialized = System.Text.Json.JsonSerializer.Serialize(liveResult);
        Assert.DoesNotContain(repositoryRoot.Replace('\\', '/'), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repositoryRoot, serialized, StringComparison.OrdinalIgnoreCase);
        AssertNoServerPathLeak(liveResult, repositoryRoot);
    }

    [Fact]
    public async Task AnalyzeAsync_SourcePreviewHonorsAggregateFileCountCap()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-preview-count-{Guid.NewGuid():N}");

        try
        {
            var solutionPath = CreateSourcePreviewLimitFixture(tempRoot);
            for (var index = 0; index < SolutionAnalysisService.SourcePreviewFileCountLimit + 20; index++)
            {
                WriteFile(Path.Combine(tempRoot, "preview", index.ToString("D3"), "README.md"), $"Preview file {index}");
            }

            var result = await CreateService().AnalyzeAsync(
                solutionPath,
                AnalysisInputTrust.UntrustedArchive,
                CancellationToken.None);

            Assert.True(result.SourcePreviewCapped);
            Assert.Equal(SolutionAnalysisService.SourcePreviewFileCountLimit, result.SourceFiles.Count);
            Assert.Equal(result.SourceFiles.Count, result.SourcePreviewIncludedFileCount);
            Assert.True(result.SourcePreviewOmittedFileCount > 0);
            Assert.Contains("omitted", result.SourcePreviewCappedReason, StringComparison.OrdinalIgnoreCase);
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
    public async Task AnalyzeAsync_SourcePreviewHonorsAggregateByteCap()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-preview-bytes-{Guid.NewGuid():N}");

        try
        {
            var solutionPath = CreateSourcePreviewLimitFixture(tempRoot);
            var largePreview = new string('x', 650_000);
            for (var index = 0; index < 10; index++)
            {
                WriteFile(Path.Combine(tempRoot, "preview", index.ToString("D2"), "README.md"), largePreview);
            }

            var result = await CreateService().AnalyzeAsync(
                solutionPath,
                AnalysisInputTrust.UntrustedArchive,
                CancellationToken.None);

            Assert.True(result.SourcePreviewCapped);
            Assert.True(result.SourcePreviewIncludedBytes <= SolutionAnalysisService.SourcePreviewByteLimit);
            Assert.Equal(SolutionAnalysisService.SourcePreviewByteLimit, result.SourcePreviewByteLimit);
            Assert.True(result.SourcePreviewOmittedFileCount > 0);
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
    public void ApiReadinessAnalyzer_RazorPagesHostDoesNotReceiveApiEndpointOrSwaggerWarnings()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-razor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "SchemaArchitect.Web",
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddRazorPages();
                var app = builder.Build();
                app.MapRazorPages();
                app.Run();
                """,
                ("Pages/Index.cshtml", "@page\n<h1>SchemaArchitect</h1>"));

            Assert.DoesNotContain(issues, issue => issue.RuleId is "API002" or "API003");
            Assert.Contains(issues, issue =>
                issue.RuleId == "API004"
                && issue.Severity == IssueSeverity.Info
                && issue.WhyDetected?.Contains("Project intent: WebUi", StringComparison.Ordinal) == true);
            Assert.InRange(new ScoringService().CalculateCategoryScores(issues).ApiReadiness, 98, 100);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_WebUiWithOnlyInfoGuidanceHasNoActiveProductionFindings()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-clean-webui-{Guid.NewGuid():N}");
        var solutionPath = Path.Combine(tempRoot, "SchemaArchitect.slnx");
        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteFile(solutionPath, """
                <Solution>
                  <Project Path="src/SchemaArchitect.Web/SchemaArchitect.Web.csproj" />
                </Solution>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Web", "SchemaArchitect.Web.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Web", "Program.cs"), """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddRazorPages();
                var app = builder.Build();
                app.UseHttpsRedirection();
                app.MapRazorPages();
                app.Run();
                """);
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Web", "appsettings.json"), """
                {
                  "Logging": {
                    "LogLevel": {
                      "Default": "Information"
                    }
                  }
                }
                """);
            WriteFile(Path.Combine(tempRoot, "src", "SchemaArchitect.Web", "Pages", "Index.cshtml"), """
                @page
                <h1>SchemaArchitect</h1>
                """);

            var result = await CreateService().AnalyzeAsync(solutionPath, CancellationToken.None);
            var activeProductionFindings = result.Issues
                .Where(AnalyzerUtilities.IsActiveProductionFinding)
                .ToArray();

            Assert.Empty(activeProductionFindings);
            Assert.Equal(100, result.CategoryScores.ApiReadiness);
            Assert.Equal(100, result.OverallScore);
            Assert.DoesNotContain(result.Issues, issue => issue.RuleId is "API002" or "API003");
            Assert.All(result.Issues.Where(issue => issue.Category == AnalysisCategories.ApiReadiness), issue =>
                Assert.Equal(IssueSeverity.Info, issue.Severity));
            Assert.DoesNotContain(result.EngineeringAssessment!.HighestRisks, risk =>
                risk.Contains("API reliability", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("No active production root-cause findings were detected", result.EngineeringAssessment!.ScoreExplanation);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApiReadinessAnalyzer_MvcUiHostDoesNotReceiveApiEndpointOrSwaggerWarnings()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-mvc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "Storefront.Web",
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllersWithViews();
                var app = builder.Build();
                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                app.Run();
                """,
                ("Controllers/HomeController.cs", """
                using Microsoft.AspNetCore.Mvc;

                public sealed class HomeController : Controller
                {
                    public IActionResult Index() => View();
                }
                """),
                ("Views/Home/Index.cshtml", "<h1>Home</h1>"));

            Assert.DoesNotContain(issues, issue => issue.RuleId is "API002" or "API003");
            Assert.All(issues.Where(issue => issue.RuleId is "API004" or "API005"), issue =>
                Assert.Equal(IssueSeverity.Info, issue.Severity));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApiReadinessAnalyzer_MvcUiSupportApiControllerDoesNotReceiveSwaggerWarning()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-support-controller-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "Storefront.Web",
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllersWithViews();
                builder.Services.AddRazorPages();
                var app = builder.Build();
                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                app.MapRazorPages();
                app.Run();
                """,
                ("Controllers/UserController.cs", """
                using Microsoft.AspNetCore.Mvc;

                [ApiController]
                [Route("[controller]")]
                public sealed class UserController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult GetCurrentUser() => Ok();
                }
                """),
                ("Views/Home/Index.cshtml", "<h1>Home</h1>"),
                ("Pages/Index.cshtml", "@page\n<h1>Home</h1>"));

            Assert.DoesNotContain(issues, issue => issue.RuleId == "API003");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApiReadinessAnalyzer_UnusedApiBaseControllerDoesNotReceiveSwaggerWarning()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-base-controller-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "Storefront.Web",
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllersWithViews();
                var app = builder.Build();
                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                app.Run();
                """,
                ("Controllers/Api/BaseApiController.cs", """
                using Microsoft.AspNetCore.Mvc;

                [ApiController]
                [Route("api/[controller]/[action]")]
                public abstract class BaseApiController : ControllerBase
                {
                }
                """),
                ("Views/Home/Index.cshtml", "<h1>Home</h1>"));

            Assert.DoesNotContain(issues, issue => issue.RuleId == "API003");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApiReadinessAnalyzer_BlazorHostDoesNotReceiveApiEndpointOrSwaggerWarnings()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-blazor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "BackOffice.Blazor",
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddRazorComponents();
                var app = builder.Build();
                app.MapRazorComponents<App>();
                app.Run();
                """,
                ("Components/App.razor", "<Router AppAssembly=\"typeof(Program).Assembly\" />"));

            Assert.DoesNotContain(issues, issue => issue.RuleId is "API002" or "API003");
            Assert.Contains(issues, issue =>
                issue.RuleId == "API004"
                && issue.Severity == IssueSeverity.Info
                && issue.WhyDetected?.Contains("Project intent: WebUi", StringComparison.Ordinal) == true);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApiReadinessAnalyzer_MinimalApiStillReceivesSwaggerReadinessWarning()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-minimal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "Catalog.Api",
                """
                var builder = WebApplication.CreateBuilder(args);
                var app = builder.Build();
                app.MapGet("/catalog", () => Results.Ok());
                app.Run();
                """);

            Assert.DoesNotContain(issues, issue => issue.RuleId == "API002");
            Assert.Contains(issues, issue =>
                issue.RuleId == "API003"
                && issue.Severity == IssueSeverity.Warning
                && issue.WhyDetected?.Contains("Project intent: Api", StringComparison.Ordinal) == true);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApiReadinessAnalyzer_ControllerApiStillReceivesSwaggerReadinessWarning()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-controller-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "Orders.Api",
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllers();
                var app = builder.Build();
                app.MapControllers();
                app.Run();
                """,
                ("Controllers/OrdersController.cs", """
                using Microsoft.AspNetCore.Mvc;

                [ApiController]
                [Route("api/orders")]
                public sealed class OrdersController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult Get() => Ok();
                }
                """));

            Assert.DoesNotContain(issues, issue => issue.RuleId == "API002");
            Assert.Contains(issues, issue =>
                issue.RuleId == "API003"
                && issue.Severity == IssueSeverity.Warning
                && issue.WhyDetected?.Contains("Project intent: Api", StringComparison.Ordinal) == true);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApiReadinessAnalyzer_MixedUiAndApiHostKeepsApiReadinessChecks()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-mixed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "Portal.Web",
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllersWithViews();
                var app = builder.Build();
                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                app.MapControllers();
                app.Run();
                """,
                ("Views/Home/Index.cshtml", "<h1>Portal</h1>"),
                ("Controllers/StatusController.cs", """
                using Microsoft.AspNetCore.Mvc;

                [ApiController]
                [Route("api/status")]
                public sealed class StatusController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult Get() => Ok();
                }
                """));

            Assert.DoesNotContain(issues, issue => issue.RuleId == "API002");
            Assert.Contains(issues, issue =>
                issue.RuleId == "API003"
                && issue.Severity == IssueSeverity.Warning
                && issue.WhyDetected?.Contains("Project intent: MixedApiAndWebUi", StringComparison.Ordinal) == true);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApiReadinessAnalyzer_MixedUiAndMinimalApiHostKeepsSwaggerReadinessWarning()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-mixed-minimal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "Portal.Web",
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllersWithViews();
                var app = builder.Build();
                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                app.MapGet("/api/status", () => Results.Ok());
                app.Run();
                """,
                ("Views/Home/Index.cshtml", "<h1>Portal</h1>"));

            Assert.Contains(issues, issue =>
                issue.RuleId == "API003"
                && issue.Severity == IssueSeverity.Warning
                && issue.WhyDetected?.Contains("Project intent: MixedApiAndWebUi", StringComparison.Ordinal) == true);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApiReadinessAnalyzer_MixedUiAndApiRouteActionsKeepSwaggerReadinessWarning()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-api-intent-mixed-route-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var issues = AnalyzeApiReadinessProject(
                tempRoot,
                "Portal.Web",
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllersWithViews();
                var app = builder.Build();
                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                app.Run();
                """,
                ("Views/Home/Index.cshtml", "<h1>Portal</h1>"),
                ("Controllers/StatusController.cs", """
                using Microsoft.AspNetCore.Mvc;

                [ApiController]
                [Route("api/status")]
                public sealed class StatusController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult Get() => Ok();
                }
                """));

            Assert.Contains(issues, issue =>
                issue.RuleId == "API003"
                && issue.Severity == IssueSeverity.Warning
                && issue.WhyDetected?.Contains("Project intent: MixedApiAndWebUi", StringComparison.Ordinal) == true);
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
    public async Task AnalyzeAsync_DiIgnoresFrameworkProvidedConstructorDependencies()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-di-framework-{Guid.NewGuid():N}");
        var solutionPath = Path.Combine(tempRoot, "DiFramework.slnx");
        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteFile(solutionPath, """
                <Solution>
                  <Project Path="src/DiFramework.Api/DiFramework.Api.csproj" />
                </Solution>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "DiFramework.Api", "DiFramework.Api.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "DiFramework.Api", "Program.cs"), """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllers();
                builder.Services.AddScoped<CustomAuthStateProvider>();
                var app = builder.Build();
                app.MapControllers();
                app.Run();
                """);
            WriteFile(Path.Combine(tempRoot, "src", "DiFramework.Api", "Services.cs"), """
                using Microsoft.Extensions.Logging;
                using Microsoft.Extensions.Options;

                public sealed class BaseUrlConfiguration
                {
                    public string BaseUrl { get; set; } = "";
                }

                public interface ICustomDependency { }

                public sealed class CustomAuthStateProvider
                {
                    public CustomAuthStateProvider(
                        ILogger<CustomAuthStateProvider> logger,
                        IOptions<BaseUrlConfiguration> options)
                    {
                    }
                }
                """);
            WriteFile(Path.Combine(tempRoot, "src", "DiFramework.Api", "Controllers", "SampleController.cs"), """
                using Microsoft.AspNetCore.Mvc;
                using Microsoft.Extensions.Logging;

                public sealed class SampleController : ControllerBase
                {
                    public SampleController(
                        ILoggerFactory loggerFactory,
                        ICustomDependency customDependency)
                    {
                    }
                }
                """);

            var result = await CreateService().AnalyzeAsync(solutionPath, CancellationToken.None);
            var di002Issues = result.Issues
                .Where(issue => issue.RuleId == "DI002")
                .ToArray();

            Assert.DoesNotContain(di002Issues, issue => issue.Title.Contains("ILogger", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(di002Issues, issue => issue.Title.Contains("ILoggerFactory", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(di002Issues, issue => issue.Title.Contains("IOptions", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(di002Issues, issue => issue.Title == "ICustomDependency appears unregistered");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_DiDoesNotFlagMediatorWhenAddMediatRIsPresent()
    {
        var result = await AnalyzeMediatRProjectAsync("builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));");

        Assert.DoesNotContain(result.Issues, issue =>
            issue.RuleId == "DI002"
            && issue.Title.Contains("IMediator", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeAsync_DiDoesNotFlagMediatorWhenSourceExtensionWrapsAddMediatR()
    {
        var result = await AnalyzeMediatRProjectAsync(
            "builder.Services.AddApplicationMediator();",
            """
            using Microsoft.Extensions.DependencyInjection;

            public static class ApplicationMediatorRegistration
            {
                public static IServiceCollection AddApplicationMediator(this IServiceCollection services)
                {
                    services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateOrderHandler>());
                    return services;
                }
            }

            public sealed class CreateOrderHandler { }
            """);

        Assert.DoesNotContain(result.Issues, issue =>
            issue.RuleId == "DI002"
            && issue.Title.Contains("IMediator", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeAsync_DiFlagsMediatorWhenNoMediatRRegistrationEvidenceExists()
    {
        var result = await AnalyzeMediatRProjectAsync(string.Empty);

        Assert.Contains(result.Issues, issue =>
            issue.RuleId == "DI002"
            && issue.Title == "IMediator appears unregistered");
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

        Assert.Equal("Roslyn Semantic Analysis", result.AnalysisFidelity);
        Assert.False(result.SemanticAnalysisSkipped);
        Assert.Null(result.SemanticAnalysisSkippedReason);
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
    public async Task AnalyzeAsync_UntrustedArchiveSkipsSemanticProjectLoading()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-untrusted-{Guid.NewGuid():N}");
        var solutionPath = Path.Combine(tempRoot, "Untrusted.slnx");
        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteFile(solutionPath, """
                <Solution>
                  <Project Path="src/Untrusted.Api/Untrusted.Api.csproj" />
                </Solution>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "Untrusted.Api", "Untrusted.Api.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "Untrusted.Api", "Program.cs"), """
                var builder = WebApplication.CreateBuilder(args);
                var app = builder.Build();
                app.MapGet("/api/health", () => Results.Ok());
                app.Run();
                """);

            var result = await CreateService().AnalyzeAsync(
                solutionPath,
                AnalysisInputTrust.UntrustedArchive,
                CancellationToken.None);

            Assert.Equal("Safe Syntax Analysis", result.AnalysisFidelity);
            Assert.True(result.SemanticAnalysisSkipped);
            Assert.Contains("Semantic project loading was skipped", result.SemanticAnalysisSkippedReason);
            Assert.DoesNotContain(result.Issues, issue =>
                issue.DetectionMethod == IssueEnrichmentService.RoslynSemanticAnalysis);
            Assert.Contains(result.ProjectGraph.Projects, project => project.Name == "Untrusted.Api");
            Assert.Contains(result.SourceFiles, file =>
                file.RelativePath == "src/Untrusted.Api/Program.cs"
                && file.Content.Contains("MapGet", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
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

    private static IReadOnlyList<AnalysisIssue> AnalyzeApiReadinessProject(
        string root,
        string projectName,
        string programSource,
        params (string RelativePath, string Content)[] additionalFiles)
    {
        var projectDirectory = Path.Combine(root, "src", projectName);
        var projectPath = Path.Combine(projectDirectory, $"{projectName}.csproj");
        WriteFile(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(Path.Combine(projectDirectory, "Program.cs"), programSource);

        foreach (var (relativePath, content) in additionalFiles)
        {
            WriteFile(Path.Combine(projectDirectory, relativePath), content);
        }

        var project = CreateAnalyzedProject(projectName, AnalyzerUtilities.PresentationLayer, isWebProject: true, isAspNetCoreEntryPoint: true) with
        {
            FilePath = projectPath,
            DirectoryPath = projectDirectory,
            Sdk = "Microsoft.NET.Sdk.Web"
        };
        var sourceFiles = Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !AnalyzerUtilities.IsUnderBuildOutput(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var text = File.ReadAllText(path);
                var tree = CSharpSyntaxTree.ParseText(text, path: path);
                return new SourceFileContext
                {
                    Project = project,
                    FilePath = AnalyzerUtilities.NormalizePath(path),
                    Text = text,
                    Root = tree.GetCompilationUnitRoot()
                };
            })
            .ToArray();
        var context = new SolutionAnalysisContext
        {
            SolutionPath = Path.Combine(root, "IntentSample.slnx"),
            SolutionName = "IntentSample",
            RootDirectory = root,
            Projects = [project],
            SourceFiles = sourceFiles,
            SemanticProjects = [],
            SemanticDocuments = [],
            AppSettingsFiles = []
        };

        return new ApiReadinessAnalyzer(new SemanticAnalysisHelper()).Analyze(context);
    }

    private static async Task<AnalysisResult> AnalyzeMediatRProjectAsync(
        string mediatRRegistrationSource,
        string? extensionSource = null)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotdet-mediatr-{Guid.NewGuid():N}");
        var solutionPath = Path.Combine(tempRoot, "MediatRSample.slnx");
        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteFile(solutionPath, """
                <Solution>
                  <Project Path="src/MediatRSample.Api/MediatRSample.Api.csproj" />
                </Solution>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "MediatRSample.Api", "MediatRSample.Api.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            WriteFile(Path.Combine(tempRoot, "src", "MediatRSample.Api", "Program.cs"), $$"""
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddControllers();
                {{mediatRRegistrationSource}}
                var app = builder.Build();
                app.MapControllers();
                app.Run();
                """);
            WriteFile(Path.Combine(tempRoot, "src", "MediatRSample.Api", "Controllers", "OrdersController.cs"), """
                using MediatR;
                using Microsoft.AspNetCore.Mvc;

                public sealed class OrdersController : ControllerBase
                {
                    public OrdersController(IMediator mediator)
                    {
                    }
                }
                """);

            if (!string.IsNullOrWhiteSpace(extensionSource))
            {
                WriteFile(Path.Combine(tempRoot, "src", "MediatRSample.Api", "MediatorRegistration.cs"), extensionSource);
            }

            return await CreateService().AnalyzeAsync(solutionPath, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
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
                    RootCauseKey = $"SEC001|Api|{sourcePath}|Configuration risk",
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

    private static void AssertRootCauseKeyIsSafe(string? rootCauseKey)
    {
        if (string.IsNullOrWhiteSpace(rootCauseKey))
        {
            return;
        }

        Assert.All(rootCauseKey.Split('|'), segment =>
        {
            Assert.False(Path.IsPathRooted(segment), $"Root-cause key contains an absolute path: {rootCauseKey}");
            Assert.DoesNotContain("..", segment, StringComparison.Ordinal);
        });
    }

    private static void AssertNoServerPathLeak(object value, params string[] serverRoots)
    {
        var element = JsonSerializer.SerializeToElement(value);
        AssertNoServerPathLeak(element, "$", serverRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .SelectMany(root => new[] { root, root.Replace('\\', '/') })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private static void AssertNoServerPathLeak(JsonElement element, string propertyPath, IReadOnlyList<string> serverRoots)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    AssertNoServerPathLeak(property.Value, $"{propertyPath}.{property.Name}", serverRoots);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    AssertNoServerPathLeak(item, $"{propertyPath}[{index++}]", serverRoots);
                }
                break;
            case JsonValueKind.String:
                var text = element.GetString() ?? string.Empty;
                Assert.DoesNotContain(serverRoots, root =>
                    text.Contains(root, StringComparison.OrdinalIgnoreCase));
                Assert.False(
                    Regex.IsMatch(text, @"\b[A-Za-z]:[\\/]"),
                    $"Serialized response contains a Windows absolute path at {propertyPath}: {text}");
                Assert.False(
                    Regex.IsMatch(text, @"(?<![:\w])/(?:tmp|home|Users|private/var|var/tmp|mnt)/", RegexOptions.IgnoreCase),
                    $"Serialized response contains a Unix server or temporary path at {propertyPath}: {text}");
                break;
        }
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

    private static string CreateSourcePreviewLimitFixture(string root)
    {
        var projectPath = Path.Combine(root, "src", "Preview.Api", "Preview.Api.csproj");
        var solutionPath = Path.Combine(root, "Preview.slnx");
        WriteFile(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile(Path.Combine(root, "src", "Preview.Api", "Program.cs"), "var builder = WebApplication.CreateBuilder(args);");
        WriteFile(solutionPath, """
            <Solution>
              <Project Path="src/Preview.Api/Preview.Api.csproj" />
            </Solution>
            """);
        return solutionPath;
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

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Forge.Api.Tests";

        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
