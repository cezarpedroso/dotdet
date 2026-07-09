using Forge.Api.Analysis;
using Forge.Api.Models;

namespace Forge.Api.Services;

public sealed class RuleCatalogService
{
    private readonly IssueEnrichmentService _issueEnrichmentService;

    public RuleCatalogService(IssueEnrichmentService issueEnrichmentService)
    {
        _issueEnrichmentService = issueEnrichmentService;
    }

    public IReadOnlyList<RuleDocumentation> GetRules()
    {
        var definitions = GetRuleDefinitions();
        var seedIssues = definitions
            .Select((definition, index) => new AnalysisIssue
            {
                Id = $"{definition.RuleId}-DOC",
                RuleId = definition.RuleId,
                Title = definition.Title,
                Description = definition.DetectionLogic,
                Severity = definition.Severity,
                Category = definition.Category,
                ProjectName = "Rule Catalog",
                FilePath = null,
                LineNumber = definition.HasSourceLocation ? 1 : null,
                Recommendation = definition.RecommendedFallback
            })
            .ToArray();
        var enrichedByRuleId = _issueEnrichmentService.Enrich(seedIssues)
            .ToDictionary(issue => issue.RuleId ?? issue.Id, StringComparer.OrdinalIgnoreCase);

        return definitions
            .Select(definition =>
            {
                var enriched = enrichedByRuleId[definition.RuleId];
                var confidence = enriched.Confidence ?? IssueConfidence.Medium;

                return new RuleDocumentation
                {
                    RuleId = definition.RuleId,
                    Title = definition.Title,
                    Category = definition.Category,
                    Severity = definition.Severity,
                    DetectionMethod = enriched.DetectionMethod ?? IssueEnrichmentService.HeuristicAnalysis,
                    Confidence = confidence,
                    ConfidenceExplanation = GetConfidenceExplanation(confidence, enriched.DetectionMethod),
                    ProblemSummary = enriched.ProblemSummary ?? definition.Title,
                    WhyItMatters = enriched.WhyItMatters ?? definition.DetectionLogic,
                    DetectionLogic = definition.DetectionLogic,
                    RecommendedPattern = enriched.RecommendedPattern ?? definition.RecommendedFallback,
                    SuggestedImplementation = enriched.SuggestedImplementation ?? definition.RecommendedFallback,
                    GoodExample = enriched.GoodExample,
                    BadExample = enriched.BadExample,
                    SuggestedCodeSnippet = enriched.SuggestedSnippet,
                    DocumentationLinks = enriched.DocumentationLinks ?? Array.Empty<AnalysisDocumentationLink>(),
                    FalsePositiveGuidance = definition.FalsePositiveGuidance,
                    RelatedRules = definition.RelatedRules
                };
            })
            .OrderBy(rule => GetCategoryOrder(rule.Category))
            .ThenBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<RuleDefinition> GetRuleDefinitions()
    {
        return
        [
            Rule("ARCH001", "Domain layer depends on an outer layer", AnalysisCategories.Architecture, IssueSeverity.Error,
                "Uses project references and semantic type references to detect Domain projects referencing Infrastructure, API, or Web projects.",
                "Confirm the project is truly a domain layer. If the naming is misleading or the referenced project only contains pure contracts, document the exception or rename the project.",
                ["ARCH002", "ARCH003", "ARCH004"]),
            Rule("ARCH002", "Domain project references framework infrastructure", AnalysisCategories.Architecture, IssueSeverity.Error,
                "Inspects package/assembly references and semantic type usage for EF Core or ASP.NET Core symbols inside Domain projects.",
                "False positives are possible when a project is named Domain but intentionally hosts framework adapters. Prefer renaming or moving adapters outward.",
                ["ARCH001", "EF001", "API002"]),
            Rule("ARCH003", "Application layer references Infrastructure directly", AnalysisCategories.Architecture, IssueSeverity.Warning,
                "Uses project reference data and semantic type tracing to find Application projects depending on Infrastructure implementations.",
                "If the referenced assembly only contains abstractions, consider moving those contracts to Application or Shared and renaming the project.",
                ["ARCH001", "DI002", "ARCH005"]),
            Rule("ARCH004", "Lower layer references API/Web project", AnalysisCategories.Architecture, IssueSeverity.Error,
                "Analyzes project references to find Domain, Application, or Infrastructure projects referencing delivery-layer API/Web projects.",
                "Review whether the target project is mislabeled. Lower layers should not need web host types or controllers.",
                ["ARCH001", "ARCH005", "API002"]),
            Rule("ARCH005", "Circular project dependency detected", AnalysisCategories.Architecture, IssueSeverity.Critical,
                "Builds a project dependency graph and performs cycle detection across project-to-project references.",
                "Generated or test-only cycles can be accepted temporarily, but production projects should break cycles through stable contracts.",
                ["ARCH003", "ARCH004"]),

            Rule("DI001", "Duplicate dependency injection registration", AnalysisCategories.DependencyInjection, IssueSeverity.Warning,
                "Inspects Program.cs, Startup.cs, and composition extension methods for repeated registrations of the same service type.",
                "Multiple implementations can be intentional when consumed as IEnumerable<T>. Document intentional multi-registration patterns.",
                ["DI002", "DI003"]),
            Rule("DI002", "Constructor dependency appears unregistered", AnalysisCategories.DependencyInjection, IssueSeverity.Warning,
                "Collects constructor-injected application service types and compares them against discovered IServiceCollection registrations.",
                "Registration may happen in external packages, reflection scanning, source generators, or runtime modules that DotDet cannot resolve yet.",
                ["DI001", "DI003", "ARCH003"]),
            Rule("DI003", "Singleton service captures scoped dependency", AnalysisCategories.DependencyInjection, IssueSeverity.Error,
                "Builds DI registration lifetimes and constructor dependencies, then detects singleton registrations that consume scoped services.",
                "Confirm the reported lifetimes are production lifetimes. Test-only registrations or factory-created scopes may be intentional.",
                ["DI001", "DI002", "EF001"]),

            Rule("EF001", "EF Core package referenced without a DbContext", AnalysisCategories.EfCore, IssueSeverity.Info,
                "Reads resolved project package references and searches the project for types inheriting from DbContext.",
                "Projects may reference EF Core only for shared configuration, design-time tooling, or abstractions. Remove the package if it is not needed at runtime.",
                ["EF002", "EF006"]),
            Rule("EF002", "DbContext has no DbSet properties", AnalysisCategories.EfCore, IssueSeverity.Info,
                "Detects DbContext types and inspects their properties for DbSet<TEntity> declarations.",
                "ModelBuilder-only contexts can be valid. Add documentation if entities are intentionally configured without DbSet properties.",
                ["EF003", "EF006"]),
            Rule("EF003", "Entity is missing an obvious primary key", AnalysisCategories.EfCore, IssueSeverity.Warning,
                "Uses DbSet<TEntity> entity symbols and checks for conventional Id, EntityNameId, [Key], or configured HasKey patterns.",
                "Keys configured through external conventions or complex model builders may not be visible to DotDet.",
                ["EF002", "EF006"]),
            Rule("EF004", "Migration contains destructive schema operation", AnalysisCategories.EfCore, IssueSeverity.Warning,
                "Inspects migration files for migrationBuilder.DropTable and migrationBuilder.DropColumn operations.",
                "Destructive changes may be safe during early development or when paired with a documented data migration and rollback plan.",
                ["EF005", "EF006"]),
            Rule("EF005", "Migration executes raw SQL", AnalysisCategories.EfCore, IssueSeverity.Warning,
                "Inspects migration files for migrationBuilder.Sql calls.",
                "Raw SQL can be necessary for provider-specific operations. Document why it is safe and how it behaves across environments.",
                ["EF004", "EF006"]),
            Rule("EF006", "DbContext project has no migrations", AnalysisCategories.EfCore, IssueSeverity.Info,
                "Finds DbContext types and checks the owning project for migration files.",
                "Some teams use external migration projects, SQL scripts, or managed database deployment tools. Document the strategy.",
                ["EF001", "EF004"]),

            Rule("SEC001", "No appsettings.json files found", AnalysisCategories.Security, IssueSeverity.Warning,
                "Checks web/API projects for appsettings.json or environment-specific appsettings files.",
                "Configuration can be supplied entirely through environment variables or platform configuration. Document that production source clearly.",
                ["SECJSON", "SECCONN", "SECJWT"]),
            Rule("SECJSON", "Configuration file is not valid JSON", AnalysisCategories.Security, IssueSeverity.Warning,
                "Parses appsettings JSON files and reports syntax failures.",
                "Generated configuration fragments may not be intended as valid standalone JSON. Exclude or rename them if they are not appsettings files.",
                ["SEC001", "SECCONN"]),
            Rule("SECCONN", "Connection string is stored in configuration", AnalysisCategories.Security, IssueSeverity.Warning,
                "Flattens appsettings JSON and detects non-placeholder values under ConnectionStrings.",
                "Local development connection strings can be acceptable if they are clearly non-production and contain no secrets.",
                ["SECSECRET", "SECJWT"]),
            Rule("SECSECRET", "Possible secret stored in configuration", AnalysisCategories.Security, IssueSeverity.Info,
                "Uses conservative keyword and value heuristics to find password, key, token, or secret-like settings in configuration files.",
                "This is intentionally heuristic. Placeholder values, local-only values, or public identifiers may be marked as accepted risk.",
                ["SECCONN", "SECJWT"]),
            Rule("SECJWT", "Weak JWT configuration value", AnalysisCategories.Security, IssueSeverity.Warning,
                "Inspects JWT-related configuration keys for empty, short, placeholder, or development-like issuer, audience, and key values.",
                "Some values may be intentionally supplied at runtime from a secret store. Ensure the committed value is not used in production.",
                ["SEC004", "SEC005", "SECSECRET"]),
            Rule("SEC002", "CORS policy allows any origin", AnalysisCategories.Security, IssueSeverity.Warning,
                "Uses semantic invocation analysis where possible to detect AllowAnyOrigin calls in startup/composition code.",
                "Public unauthenticated APIs may intentionally allow broad origins. Document the threat model and avoid credentials with wildcard origins.",
                ["SEC003", "SEC004"]),
            Rule("SEC003", "HTTPS redirection middleware is missing", AnalysisCategories.Security, IssueSeverity.Warning,
                "Searches startup pipeline code for UseHttpsRedirection.",
                "Edge TLS termination can make app-level redirection redundant. Document upstream enforcement if suppressing this rule.",
                ["SEC002", "API005"]),
            Rule("SEC004", "JWT package present without authentication middleware", AnalysisCategories.Security, IssueSeverity.Warning,
                "Combines package reference detection for JWT bearer authentication with semantic/syntax checks for UseAuthentication.",
                "Authentication can be enforced upstream or by custom middleware, but that should be documented and tested.",
                ["SECJWT", "SEC005"]),
            Rule("SEC005", "JWT package present without authorization middleware", AnalysisCategories.Security, IssueSeverity.Warning,
                "Combines JWT package detection with semantic/syntax checks for UseAuthorization.",
                "Minimal APIs or custom endpoint filters may enforce authorization differently. Confirm policies are actually applied.",
                ["SECJWT", "SEC004"]),

            Rule("API001", "No web/API project was detected in the solution", AnalysisCategories.ApiReadiness, IssueSeverity.Info,
                "Uses Web SDK, ASP.NET Core package references, and project naming signals to identify API/Web projects.",
                "Libraries, workers, and non-HTTP solutions can safely ignore this rule.",
                ["API002"]),
            Rule("API002", "Web/API project has no visible endpoints", AnalysisCategories.ApiReadiness, IssueSeverity.Warning,
                "Detects controllers, MapGet/MapPost-style minimal APIs, and endpoint mapping calls in web projects.",
                "Endpoints may be generated, mapped through external modules, or loaded dynamically. Document the composition path.",
                ["API003", "API004"]),
            Rule("API003", "Swagger/OpenAPI setup is missing", AnalysisCategories.ApiReadiness, IssueSeverity.Warning,
                "Searches service registration and endpoint mapping code for Swagger/OpenAPI setup.",
                "Internal APIs may use separate contract publication. Document the alternative API discovery mechanism.",
                ["API002", "API007"]),
            Rule("API004", "Health checks are missing", AnalysisCategories.ApiReadiness, IssueSeverity.Warning,
                "Searches for AddHealthChecks and MapHealthChecks in web/API startup code.",
                "Health checks may be provided by sidecars or platform probes. Document the operational readiness path.",
                ["API005", "API006"]),
            Rule("API005", "Global exception handling is missing", AnalysisCategories.ApiReadiness, IssueSeverity.Warning,
                "Searches for UseExceptionHandler, custom exception middleware, or centralized Problem Details setup.",
                "Some teams use gateway-level error normalization, but API hosts should still avoid leaking inconsistent exception responses.",
                ["API006", "SEC003"]),
            Rule("API006", "Structured logging setup is missing", AnalysisCategories.ApiReadiness, IssueSeverity.Info,
                "Searches host logging setup for structured providers such as JSON console logging, Serilog, or OpenTelemetry signals.",
                "Logging can be configured by hosting defaults or platform agents. Confirm logs remain queryable and correlated in production.",
                ["API004", "API005"]),
            Rule("API007", "Request validation pattern is missing", AnalysisCategories.ApiReadiness, IssueSeverity.Info,
                "Looks for FluentValidation, validation attributes, endpoint filters, or consistent model validation setup.",
                "Validation may be handled in a shared pipeline or gateway. Ensure invalid requests cannot reach business workflows.",
                ["API002", "API005"])
        ];
    }

    private static RuleDefinition Rule(
        string ruleId,
        string title,
        string category,
        IssueSeverity severity,
        string detectionLogic,
        string falsePositiveGuidance,
        IReadOnlyList<string> relatedRules,
        bool hasSourceLocation = true)
    {
        return new RuleDefinition(
            ruleId,
            title,
            category,
            severity,
            detectionLogic,
            falsePositiveGuidance,
            relatedRules,
            GetDefaultRecommendation(category),
            hasSourceLocation);
    }

    private static string GetDefaultRecommendation(string category)
    {
        return category switch
        {
            AnalysisCategories.Architecture => "Keep dependencies flowing toward stable inner layers and isolate infrastructure behind abstractions.",
            AnalysisCategories.DependencyInjection => "Make service registrations explicit, single-purpose, and lifetime-compatible.",
            AnalysisCategories.EfCore => "Keep persistence configuration explicit and review migration impact before production deployment.",
            AnalysisCategories.Security => "Use production-safe configuration, explicit middleware, and managed secret storage.",
            AnalysisCategories.ApiReadiness => "Make the API observable, documented, validated, and resilient before production release.",
            _ => "Review and remediate the production-readiness concern."
        };
    }

    private static string GetConfidenceExplanation(IssueConfidence confidence, string? detectionMethod)
    {
        return confidence switch
        {
            IssueConfidence.High => $"High confidence because this rule is usually proven through {detectionMethod ?? "explicit code or project configuration"}.",
            IssueConfidence.Medium => $"Medium confidence because this rule is strongly inferred through {detectionMethod ?? "syntax or usage patterns"} but may miss external composition.",
            IssueConfidence.Low => "Low confidence because this rule uses best-effort heuristics and should be reviewed before treating it as a release blocker.",
            _ => "Confidence indicates how reliable the detection signal is."
        };
    }

    private static int GetCategoryOrder(string category)
    {
        return category switch
        {
            AnalysisCategories.Architecture => 0,
            AnalysisCategories.DependencyInjection => 1,
            AnalysisCategories.EfCore => 2,
            AnalysisCategories.Security => 3,
            AnalysisCategories.ApiReadiness => 4,
            _ => 9
        };
    }

    private sealed record RuleDefinition(
        string RuleId,
        string Title,
        string Category,
        IssueSeverity Severity,
        string DetectionLogic,
        string FalsePositiveGuidance,
        IReadOnlyList<string> RelatedRules,
        string RecommendedFallback,
        bool HasSourceLocation);
}
