using Forge.Api.Analysis;
using Forge.Api.Models;

namespace Forge.Api.Services;

public sealed class IssueEnrichmentService
{
    public const string RoslynSemanticAnalysis = "Roslyn Semantic Analysis";
    public const string RoslynSyntaxAnalysis = "Roslyn Syntax Analysis";
    public const string MsBuildProjectConfiguration = "MSBuild / Project Configuration";
    public const string HeuristicAnalysis = "Heuristic Analysis";

    public IReadOnlyList<AnalysisIssue> Enrich(IEnumerable<AnalysisIssue> issues)
    {
        var issueArray = issues.ToArray();

        return issueArray
            .Select(issue => EnrichIssue(issue, issueArray))
            .ToArray();
    }

    private AnalysisIssue EnrichIssue(AnalysisIssue issue, IReadOnlyList<AnalysisIssue> allIssues)
    {
        var ruleId = issue.RuleId ?? GetStableRuleId(issue);
        var guidance = GetGuidance(ruleId, issue);
        var detectionMethod = issue.DetectionMethod
            ?? NormalizeGuidanceDetectionMethod(ruleId, issue, guidance.DetectionMethod)
            ?? InferDetectionMethod(issue);

        return issue with
        {
            RuleId = ruleId,
            Confidence = issue.Confidence ?? guidance.Confidence ?? InferConfidence(issue),
            DetectionMethod = detectionMethod,
            ProblemSummary = issue.ProblemSummary ?? guidance.ProblemSummary ?? issue.Title,
            WhyDetected = issue.WhyDetected ?? guidance.WhyDetected ?? issue.Description,
            WhyItMatters = issue.WhyItMatters ?? guidance.WhyItMatters ?? GetDefaultWhyItMatters(issue.Category),
            RecommendedPattern = issue.RecommendedPattern ?? guidance.RecommendedPattern ?? issue.Recommendation,
            SuggestedImplementation = issue.SuggestedImplementation ?? guidance.SuggestedImplementation ?? issue.Recommendation,
            DocumentationLinks = issue.DocumentationLinks is { Count: > 0 } ? issue.DocumentationLinks : guidance.DocumentationLinks,
            RelatedFindingIds = issue.RelatedFindingIds is { Count: > 0 } ? issue.RelatedFindingIds : GetRelatedFindingIds(issue, allIssues),
            SuggestedSnippet = issue.SuggestedSnippet ?? guidance.SuggestedSnippet,
            GoodExample = issue.GoodExample ?? guidance.GoodExample,
            BadExample = issue.BadExample ?? guidance.BadExample
        };
    }

    private static string GetStableRuleId(AnalysisIssue issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.RuleId))
        {
            return issue.RuleId;
        }

        var candidate = issue.Id.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        return issue.Category.ToUpperInvariant();
    }

    private static IReadOnlyList<string> GetRelatedFindingIds(AnalysisIssue issue, IReadOnlyList<AnalysisIssue> allIssues)
    {
        return allIssues
            .Where(candidate => candidate.Id != issue.Id)
            .Where(candidate =>
                candidate.Category == issue.Category
                || (!string.IsNullOrWhiteSpace(issue.ProjectName) && candidate.ProjectName == issue.ProjectName)
                || (!string.IsNullOrWhiteSpace(issue.FilePath) && candidate.FilePath == issue.FilePath))
            .OrderByDescending(candidate => candidate.Severity)
            .ThenBy(candidate => candidate.RuleId ?? candidate.Id, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(candidate => candidate.Id)
            .ToArray();
    }

    private static IssueGuidance GetGuidance(string ruleId, AnalysisIssue issue)
    {
        return ruleId switch
        {
            "ARCH001" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : MsBuildProjectConfiguration,
                ProblemSummary: "A Domain project references an outer infrastructure or delivery layer.",
                WhyDetected: issue.Description,
                WhyItMatters: "A domain project that directly references infrastructure or delivery code loses portability and becomes harder to test and evolve independently.",
                RecommendedPattern: "Keep the domain layer framework-agnostic and expose only domain concepts or abstractions upward.",
                SuggestedImplementation: "Move persistence or delivery contracts out of the domain assembly and have the outer layer implement abstractions defined closer to the core.",
                DocumentationLinks:
                [
                    CreateLink("Microsoft .NET architecture guidance", "https://learn.microsoft.com/dotnet/architecture/")
                ],
                SuggestedSnippet:
                """
                // Domain
                public interface IOrderRepository
                {
                    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
                }
                
                // Infrastructure
                public sealed class EfOrderRepository : IOrderRepository
                {
                }
                """,
                BadExample:
                """
                // Domain
                using MyApp.Infrastructure.Persistence;
                
                public sealed class Order
                {
                    public EfOrderRepository Repository { get; }
                }
                """,
                GoodExample:
                """
                // Domain
                public interface IOrderRepository
                {
                    Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken);
                }
                
                // Infrastructure implements the domain contract.
                """),
            "ARCH002" => new IssueGuidance(
                Confidence: issue.LineNumber is > 0 ? IssueConfidence.High : IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : MsBuildProjectConfiguration,
                ProblemSummary: "The Domain layer depends on EF Core or ASP.NET Core framework types.",
                WhyDetected: issue.Description,
                WhyItMatters: "Framework references in the domain layer make business rules depend on persistence or HTTP concerns and usually spread those dependencies through the rest of the solution.",
                RecommendedPattern: "Keep EF Core and ASP.NET Core types out of domain entities and policies.",
                SuggestedImplementation: "Move framework-specific configuration to Infrastructure or API projects and keep domain types as plain business objects.",
                DocumentationLinks:
                [
                    CreateLink("Microsoft .NET architecture guidance", "https://learn.microsoft.com/dotnet/architecture/")
                ],
                SuggestedSnippet:
                """
                // Infrastructure model configuration
                public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
                {
                    public void Configure(EntityTypeBuilder<Order> builder)
                    {
                        builder.HasKey(order => order.OrderId);
                    }
                }
                """,
                BadExample:
                """
                // Domain entity coupled to EF Core.
                public sealed class Order
                {
                    public void Configure(ModelBuilder modelBuilder) { }
                }
                """,
                GoodExample:
                """
                // Domain entity stays plain.
                public sealed class Order
                {
                    public Guid OrderId { get; set; }
                }
                """),
            "ARCH003" => new IssueGuidance(
                Confidence: issue.LineNumber is > 0 ? IssueConfidence.High : IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : MsBuildProjectConfiguration,
                ProblemSummary: "Application code depends directly on Infrastructure implementation details.",
                WhyDetected: issue.Description,
                WhyItMatters: "When application code references infrastructure directly, testing gets harder and business workflows become coupled to implementation details.",
                RecommendedPattern: "Depend on interfaces or ports in the application layer and wire infrastructure implementations at the composition root.",
                SuggestedImplementation: "Introduce an abstraction for the infrastructure dependency and inject the interface instead of the concrete type.",
                DocumentationLinks:
                [
                    CreateLink("Dependency inversion in .NET architectures", "https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles")
                ],
                SuggestedSnippet:
                """
                public interface IPaymentGateway
                {
                    Task AuthorizeAsync(string orderNumber, CancellationToken cancellationToken);
                }
                
                public sealed class OrderWorkflow
                {
                    public OrderWorkflow(IPaymentGateway paymentGateway)
                    {
                    }
                }
                """,
                BadExample:
                """
                public sealed class OrderWorkflow
                {
                    public OrderWorkflow(SqlPaymentGateway paymentGateway) { }
                }
                """,
                GoodExample:
                """
                public sealed class OrderWorkflow
                {
                    public OrderWorkflow(IPaymentGateway paymentGateway) { }
                }
                """),
            "ARCH004" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : MsBuildProjectConfiguration,
                ProblemSummary: "A lower layer references an API or Web project.",
                WhyDetected: issue.Description,
                WhyItMatters: "Lower layers should not know about outer delivery projects because it reverses dependency direction and makes the graph fragile.",
                RecommendedPattern: "Push shared contracts inward or expose them through application abstractions instead of referencing the API layer.",
                SuggestedImplementation: "Move shared DTOs or service contracts to a lower layer and remove the reference back to the delivery host.",
                DocumentationLinks:
                [
                    CreateLink("Microsoft .NET architecture guidance", "https://learn.microsoft.com/dotnet/architecture/")
                ]),
            "ARCH005" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: MsBuildProjectConfiguration,
                ProblemSummary: "The project dependency graph contains a cycle.",
                WhyDetected: issue.Description,
                WhyItMatters: "Project cycles block clean layering and make builds, testing, and refactoring much more brittle.",
                RecommendedPattern: "Break cyclic references by extracting a stable shared abstraction or moving responsibilities downward.",
                SuggestedImplementation: "Identify one reference in the cycle that can be replaced with an interface or a shared contracts project.",
                DocumentationLinks:
                [
                    CreateLink("Dependency inversion in .NET architectures", "https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles")
                ]),
            "DI001" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "A service is registered more than once in composition code.",
                WhyDetected: issue.Description,
                WhyItMatters: "Duplicate registrations can lead to unexpected implementation selection, duplicate decorators, or lifetime confusion at runtime.",
                RecommendedPattern: "Register each service/implementation pair once unless multiple implementations are intentional and documented.",
                SuggestedImplementation: "Remove duplicate registrations or replace them with a deliberate multi-registration pattern such as `IEnumerable<T>`.",
                DocumentationLinks:
                [
                    CreateLink("ASP.NET Core dependency injection", "https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection")
                ]),
            "DI002" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "A constructor-injected application dependency does not have a matching registration.",
                WhyDetected: issue.Description,
                WhyItMatters: "Missing registrations usually surface as startup exceptions or hard-to-trace runtime failures when the container builds the object graph.",
                RecommendedPattern: "Every constructor-injected application dependency should be registered in the composition root or a clearly imported module.",
                SuggestedImplementation: "Register the interface and implementation in `Program.cs`, `Startup.cs`, or a source-defined DI extension.",
                DocumentationLinks:
                [
                    CreateLink("ASP.NET Core dependency injection", "https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection")
                ],
                SuggestedSnippet: """builder.Services.AddScoped<IServiceContract, ServiceImplementation>();""",
                BadExample:
                """
                public sealed class OrderWorkflow
                {
                    public OrderWorkflow(IPaymentGateway gateway) { }
                }
                // No IPaymentGateway registration found.
                """,
                GoodExample:
                """
                builder.Services.AddScoped<IPaymentGateway, StripePaymentGateway>();
                builder.Services.AddScoped<IOrderWorkflow, OrderWorkflow>();
                """),
            "DI003" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: RoslynSemanticAnalysis,
                ProblemSummary: "A singleton service depends on a scoped service.",
                WhyDetected: issue.Description,
                WhyItMatters: "A singleton captures its constructor dependencies for the lifetime of the application. Capturing scoped services such as DbContext can leak request state, break disposal, and cause concurrency bugs.",
                RecommendedPattern: "Do not inject scoped services into singletons. Use scoped service lifetimes, IServiceScopeFactory, or move the work into a scoped operation.",
                SuggestedImplementation: "Change the consumer lifetime to scoped, or inject IServiceScopeFactory and create a scope only when background work executes.",
                DocumentationLinks:
                [
                    CreateLink("Dependency injection guidelines", "https://learn.microsoft.com/dotnet/core/extensions/dependency-injection-guidelines"),
                    CreateLink("ASP.NET Core dependency injection", "https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection")
                ],
                SuggestedSnippet:
                """
                builder.Services.AddScoped<OrderExportJob>();
                
                // Or for background/singleton work:
                public sealed class OrderExportWorker(IServiceScopeFactory scopeFactory)
                {
                    public async Task RunAsync(CancellationToken cancellationToken)
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<SampleShopDbContext>();
                    }
                }
                """,
                BadExample:
                """
                builder.Services.AddDbContext<SampleShopDbContext>();
                builder.Services.AddSingleton<OrderExportJob>();
                
                public sealed class OrderExportJob(SampleShopDbContext dbContext) { }
                """,
                GoodExample:
                """
                builder.Services.AddDbContext<SampleShopDbContext>();
                builder.Services.AddScoped<OrderExportJob>();
                """),
            "EF001" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: MsBuildProjectConfiguration,
                ProblemSummary: "A project references EF Core but no DbContext was found in that project.",
                WhyDetected: issue.Description,
                WhyItMatters: "An EF Core reference without a visible context often means unused dependencies or an incomplete persistence setup.",
                RecommendedPattern: "Keep EF Core references local to projects that own a real `DbContext` or migration strategy.",
                SuggestedImplementation: "Remove the package if unused or add the intended context and registration explicitly.",
                DocumentationLinks:
                [
                    CreateLink("DbContext in EF Core", "https://learn.microsoft.com/ef/core/dbcontext-configuration/")
                ]),
            "EF002" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "A DbContext does not expose visible DbSet properties.",
                WhyDetected: issue.Description,
                WhyItMatters: "A `DbContext` without visible `DbSet` roots can hide the intended persistence model and make the boundary unclear for maintainers.",
                RecommendedPattern: "Expose aggregate roots explicitly through `DbSet<TEntity>` properties or document a model-builder-only context.",
                SuggestedImplementation: "Add `DbSet<TEntity>` properties for the entities this context owns or document why the context is intentionally indirect.",
                DocumentationLinks:
                [
                    CreateLink("DbContext in EF Core", "https://learn.microsoft.com/ef/core/dbcontext-configuration/")
                ]),
            "EF003" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "An entity exposed by a DbContext has no obvious primary key.",
                WhyDetected: issue.Description,
                WhyItMatters: "Entities without an obvious primary key frequently fail migrations or rely on configuration that is easy to miss during review.",
                RecommendedPattern: "Use a conventional key name or configure the key explicitly in model configuration.",
                SuggestedImplementation: "Add an `Id`/`EntityNameId` property or configure `HasKey(...)` in `OnModelCreating` or an entity configuration type.",
                DocumentationLinks:
                [
                    CreateLink("Keys in EF Core", "https://learn.microsoft.com/ef/core/modeling/keys")
                ],
                SuggestedSnippet:
                """
                public sealed class CatalogItem
                {
                    public Guid CatalogItemId { get; set; }
                }
                """,
                BadExample:
                """
                public sealed class CatalogItem
                {
                    public string Sku { get; set; } = "";
                }
                """,
                GoodExample:
                """
                public sealed class CatalogItem
                {
                    public Guid CatalogItemId { get; set; }
                    public string Sku { get; set; } = "";
                }
                """),
            "EF004" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "An EF Core migration contains a destructive schema operation.",
                WhyDetected: issue.Description,
                WhyItMatters: "Destructive migrations can remove live data or block deployments if they are not rolled out with backups and a stepwise migration plan.",
                RecommendedPattern: "Prefer expand/contract migrations or explicit data migration steps before dropping schema objects.",
                SuggestedImplementation: "Review whether data should be backfilled, archived, or migrated before the destructive operation is applied.",
                DocumentationLinks:
                [
                    CreateLink("Managing migrations in EF Core", "https://learn.microsoft.com/ef/core/managing-schemas/migrations/")
                ],
                SuggestedSnippet:
                """
                // 1. Add replacement column/table
                // 2. Backfill data
                // 3. Deploy and verify
                // 4. Drop the old schema in a later migration
                """,
                BadExample:
                """
                migrationBuilder.DropColumn(
                    name: "LegacyCustomerId",
                    table: "Orders");
                """,
                GoodExample:
                """
                migrationBuilder.AddColumn<string>(
                    name: "ExternalCustomerId",
                    table: "Orders",
                    nullable: true);
                
                // Backfill and deploy before dropping the old column in a later release.
                """),
            "EF005" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "An EF Core migration executes raw SQL.",
                WhyDetected: issue.Description,
                WhyItMatters: "Raw SQL in migrations can be provider-specific, harder to review, and riskier to run repeatedly across environments.",
                RecommendedPattern: "Prefer EF Core migration APIs where practical and isolate unavoidable SQL with explicit comments and guardrails.",
                SuggestedImplementation: "Document why the SQL is required and confirm it is idempotent or safe across the target environments.",
                DocumentationLinks:
                [
                    CreateLink("Managing migrations in EF Core", "https://learn.microsoft.com/ef/core/managing-schemas/migrations/")
                ]),
            "EF006" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: HeuristicAnalysis,
                ProblemSummary: "A project contains DbContext classes but no visible migration files.",
                WhyDetected: issue.Description,
                WhyItMatters: "A project with contexts but no visible migrations may rely on an undocumented deployment path or an incomplete persistence workflow.",
                RecommendedPattern: "Keep the migration story explicit for each database-backed context.",
                SuggestedImplementation: "Add migrations, document an external migration owner, or exclude the context from production scoring if appropriate.",
                DocumentationLinks:
                [
                    CreateLink("Managing migrations in EF Core", "https://learn.microsoft.com/ef/core/managing-schemas/migrations/")
                ]),
            "SEC001" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: MsBuildProjectConfiguration,
                ProblemSummary: "No appsettings files were found for a web/API solution.",
                WhyDetected: issue.Description,
                WhyItMatters: "Missing structured configuration files makes environment-specific behavior harder to review and often hides how production settings are supplied.",
                RecommendedPattern: "Keep configuration sources explicit and document how production values are provided.",
                SuggestedImplementation: "Add `appsettings.json` or environment-specific configuration files, or document the external configuration source.",
                DocumentationLinks:
                [
                    CreateLink("Configuration in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/fundamentals/configuration/")
                ]),
            "SECJSON" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: MsBuildProjectConfiguration,
                ProblemSummary: "A configuration file could not be parsed as JSON.",
                WhyDetected: issue.Description,
                WhyItMatters: "Invalid JSON blocks configuration binding and can cause startup failures or silently missing settings at runtime.",
                RecommendedPattern: "Keep committed configuration files parseable and validate them in local tooling or CI.",
                SuggestedImplementation: "Fix the malformed JSON and rerun analysis so configuration-dependent rules can execute accurately.",
                DocumentationLinks:
                [
                    CreateLink("Configuration in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/fundamentals/configuration/")
                ]),
            "SECCONN" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: HeuristicAnalysis,
                ProblemSummary: "A committed configuration file contains a connection string value.",
                WhyDetected: issue.Description,
                WhyItMatters: "Source-controlled connection strings can leak credentials, database topology, or privileged connection details across environments.",
                RecommendedPattern: "Store production secrets outside committed configuration using environment variables, user secrets, or a managed secret store.",
                SuggestedImplementation: "Keep only non-sensitive placeholders in `appsettings.json` and bind the real value at deployment time.",
                DocumentationLinks:
                [
                    CreateLink("Safe storage of app secrets in development", "https://learn.microsoft.com/aspnet/core/security/app-secrets"),
                    CreateLink("Azure Key Vault configuration provider", "https://learn.microsoft.com/aspnet/core/security/key-vault-configuration")
                ],
                SuggestedSnippet:
                """
                "ConnectionStrings": {
                  "SampleShop": ""
                }
                // Supply the real value from user secrets, environment variables, or Key Vault.
                """,
                BadExample:
                """
                "ConnectionStrings": {
                  "Default": "Server=tcp:prod;User Id=app;Password=P@ssw0rd!"
                }
                """,
                GoodExample:
                """
                "ConnectionStrings": {
                  "Default": ""
                }
                // DOTNET_ConnectionStrings__Default is supplied by deployment configuration.
                """),
            "SECSECRET" => new IssueGuidance(
                Confidence: IssueConfidence.Low,
                DetectionMethod: HeuristicAnalysis,
                ProblemSummary: "A configuration key looks sensitive and contains a non-placeholder value.",
                WhyDetected: issue.Description,
                WhyItMatters: "Secrets committed into configuration files are easy to copy, leak, or reuse in ways that weaken production controls.",
                RecommendedPattern: "Keep API keys, tokens, and passwords in managed secret storage rather than source-controlled files.",
                SuggestedImplementation: "Replace the committed value with a placeholder and load the real secret from environment-specific secure configuration.",
                DocumentationLinks:
                [
                    CreateLink("Safe storage of app secrets in development", "https://learn.microsoft.com/aspnet/core/security/app-secrets")
                ]),
            "SECJWT" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: HeuristicAnalysis,
                ProblemSummary: "JWT issuer, audience, or signing key configuration appears weak.",
                WhyDetected: issue.Description,
                WhyItMatters: "Weak JWT settings can make issued tokens unverifiable, ambiguous across environments, or vulnerable to replay and signing issues.",
                RecommendedPattern: "Use non-empty issuer and audience values plus a high-entropy signing key supplied from secure configuration.",
                SuggestedImplementation: "Bind a strongly typed JWT options object and validate it at startup so weak production settings fail fast.",
                DocumentationLinks:
                [
                    CreateLink("JWT bearer authentication in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication")
                ],
                SuggestedSnippet:
                """
                "Jwt": {
                  "Issuer": "dotdet-api",
                  "Audience": "dotdet-clients",
                  "Key": "use-a-long-random-value-from-secure-configuration"
                }
                """,
                BadExample:
                """
                "Jwt": {
                  "Issuer": "",
                  "Audience": "",
                  "Key": "dev"
                }
                """,
                GoodExample:
                """
                "Jwt": {
                  "Issuer": "https://identity.example.com",
                  "Audience": "dotdet-api",
                  "Key": ""
                }
                // Signing key supplied from a managed secret store.
                """),
            "SEC002" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "A CORS policy allows requests from any browser origin.",
                WhyDetected: issue.Description,
                WhyItMatters: "Allowing any origin broadens who can call the API from a browser and is especially risky for authenticated or sensitive endpoints.",
                RecommendedPattern: "Restrict CORS by environment and explicitly list trusted origins.",
                SuggestedImplementation: "Define a named production policy with `WithOrigins(...)` and apply it only where needed.",
                DocumentationLinks:
                [
                    CreateLink("CORS in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/security/cors")
                ],
                SuggestedSnippet:
                """
                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("ProductionCors", policy =>
                        policy.WithOrigins("https://app.example.com")
                              .AllowAnyHeader()
                              .AllowAnyMethod());
                });
                """),
            "SEC003" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "The request pipeline does not appear to redirect HTTP traffic to HTTPS.",
                WhyDetected: issue.Description,
                WhyItMatters: "Without HTTPS redirection, clients may reach endpoints over insecure transport unless every upstream hop enforces TLS correctly.",
                RecommendedPattern: "Redirect HTTP traffic to HTTPS in the application unless a documented edge component fully enforces TLS on its behalf.",
                SuggestedImplementation: "Add `app.UseHttpsRedirection()` early in the pipeline after the app is built.",
                DocumentationLinks:
                [
                    CreateLink("Enforce HTTPS in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/security/enforcing-ssl")
                ],
                SuggestedSnippet: """app.UseHttpsRedirection();"""),
            "SEC004" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "JWT packages are present, but authentication middleware is not enabled.",
                WhyDetected: issue.Description,
                WhyItMatters: "Registering JWT support without enabling authentication middleware leaves the application misconfigured and can create a false sense of protection.",
                RecommendedPattern: "Call `UseAuthentication()` before authorization and before mapping protected endpoints.",
                SuggestedImplementation: "Ensure the request pipeline invokes authentication after CORS/HTTPS and before `UseAuthorization()`.",
                DocumentationLinks:
                [
                    CreateLink("JWT bearer authentication in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication")
                ],
                SuggestedSnippet:
                """
                app.UseAuthentication();
                app.UseAuthorization();
                """,
                BadExample:
                """
                builder.Services.AddAuthentication().AddJwtBearer();
                
                app.UseAuthorization();
                app.MapControllers();
                """,
                GoodExample:
                """
                builder.Services.AddAuthentication().AddJwtBearer();
                
                app.UseAuthentication();
                app.UseAuthorization();
                app.MapControllers();
                """),
            "SEC005" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "JWT packages are present, but authorization middleware is not enabled.",
                WhyDetected: issue.Description,
                WhyItMatters: "Without authorization middleware, authentication alone does not consistently enforce policies or authorization attributes.",
                RecommendedPattern: "Enable authorization middleware and protect endpoints with policies or authorization attributes.",
                SuggestedImplementation: "Call `UseAuthorization()` after `UseAuthentication()` and before mapping endpoints.",
                DocumentationLinks:
                [
                    CreateLink("Authorization in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/security/authorization/introduction")
                ],
                SuggestedSnippet: """app.UseAuthorization();"""),
            "API001" => new IssueGuidance(
                Confidence: IssueConfidence.High,
                DetectionMethod: MsBuildProjectConfiguration,
                ProblemSummary: "No web/API project was detected in the solution.",
                WhyDetected: issue.Description,
                WhyItMatters: "If the solution is expected to expose HTTP endpoints, a missing API host means the readiness score may not reflect the intended production surface.",
                RecommendedPattern: "Mark web-facing projects clearly with the Web SDK or ASP.NET Core packages.",
                SuggestedImplementation: "Confirm whether this solution is intentionally non-HTTP or add the expected API host project to the solution graph.",
                DocumentationLinks:
                [
                    CreateLink("ASP.NET Core web API guidance", "https://learn.microsoft.com/aspnet/core/web-api/")
                ]),
            "API002" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "A web host was detected, but no endpoints were found.",
                WhyDetected: issue.Description,
                WhyItMatters: "A web host without visible endpoints is difficult to score correctly and may indicate an incomplete or misclassified service.",
                RecommendedPattern: "Keep route mappings visible through controllers or minimal API registrations.",
                SuggestedImplementation: "Add endpoint definitions or exclude host-only projects from API readiness analysis.",
                DocumentationLinks:
                [
                    CreateLink("ASP.NET Core web API guidance", "https://learn.microsoft.com/aspnet/core/web-api/")
                ]),
            "API003" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "OpenAPI/Swagger setup was not found.",
                WhyDetected: issue.Description,
                WhyItMatters: "OpenAPI support improves discoverability, contract review, and consumer onboarding for production APIs.",
                RecommendedPattern: "Register OpenAPI generation and expose the document in appropriate environments.",
                SuggestedImplementation: "Use the built-in OpenAPI services or SwaggerGen and map the document in development or controlled production scenarios.",
                DocumentationLinks:
                [
                    CreateLink("OpenAPI support in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview")
                ],
                SuggestedSnippet:
                """
                builder.Services.AddOpenApi();
                
                if (app.Environment.IsDevelopment())
                {
                    app.MapOpenApi();
                }
                """),
            "API004" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "Health check registration and mapping were not found.",
                WhyDetected: issue.Description,
                WhyItMatters: "Health checks help orchestrators, load balancers, and operators determine whether a service is ready and alive in production.",
                RecommendedPattern: "Register health checks centrally and map a consistent readiness endpoint.",
                SuggestedImplementation: "Add `AddHealthChecks()` during service registration and `MapHealthChecks(...)` on the app.",
                DocumentationLinks:
                [
                    CreateLink("Health checks in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks")
                ],
                SuggestedSnippet:
                """
                builder.Services.AddHealthChecks();
                app.MapHealthChecks("/health");
                """),
            "API005" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "The API host does not appear to have centralized exception handling.",
                WhyDetected: issue.Description,
                WhyItMatters: "Without centralized exception handling, production errors often leak inconsistent responses and reduce observability during incidents.",
                RecommendedPattern: "Handle unhandled exceptions at the host boundary and return a consistent error format such as Problem Details.",
                SuggestedImplementation: "Register Problem Details and add centralized exception handling in the request pipeline.",
                DocumentationLinks:
                [
                    CreateLink("Handle errors in ASP.NET Core APIs", "https://learn.microsoft.com/aspnet/core/web-api/handle-errors")
                ],
                SuggestedSnippet:
                """
                builder.Services.AddProblemDetails();
                app.UseExceptionHandler();
                """,
                BadExample:
                """
                app.MapControllers();
                // Unhandled exceptions flow through the default developer/runtime behavior.
                """,
                GoodExample:
                """
                builder.Services.AddProblemDetails();
                
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler();
                }
                app.MapControllers();
                """),
            "API006" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "Structured logging setup was not found.",
                WhyDetected: issue.Description,
                WhyItMatters: "Structured logs make it much easier to search, correlate, and alert on API behavior in production.",
                RecommendedPattern: "Emit structured logs through JSON console logging, OpenTelemetry, Serilog, or source-generated logging APIs.",
                SuggestedImplementation: "Choose one structured logging path and make it part of the default host configuration.",
                DocumentationLinks:
                [
                    CreateLink("Logging in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/fundamentals/logging/")
                ],
                SuggestedSnippet:
                """
                builder.Logging.ClearProviders();
                builder.Logging.AddJsonConsole();
                """),
            "API007" => new IssueGuidance(
                Confidence: IssueConfidence.Medium,
                DetectionMethod: issue.LineNumber is > 0 ? RoslynSemanticAnalysis : RoslynSyntaxAnalysis,
                ProblemSummary: "A consistent request validation pattern was not found.",
                WhyDetected: issue.Description,
                WhyItMatters: "Consistent validation keeps malformed requests from flowing into business logic and makes API failures predictable for clients.",
                RecommendedPattern: "Validate request models through a single, explicit approach such as FluentValidation, data annotations, or endpoint filters.",
                SuggestedImplementation: "Adopt one validation path and make it part of the API composition root rather than handling validation ad hoc in controllers.",
                DocumentationLinks:
                [
                    CreateLink("Model validation in ASP.NET Core", "https://learn.microsoft.com/aspnet/core/mvc/models/validation")
                ]),
            _ => new IssueGuidance(
                DocumentationLinks:
                [
                    CreateLink(GetDefaultDocumentationLabel(issue.Category), GetDefaultDocumentationHref(issue.Category))
                ])
        };
    }

    private static IssueConfidence InferConfidence(AnalysisIssue issue)
    {
        if (issue.LineNumber is > 0
            && (issue.Description.Contains("traced", StringComparison.OrdinalIgnoreCase)
                || issue.Description.Contains("direct", StringComparison.OrdinalIgnoreCase)
                || issue.Category is AnalysisCategories.DependencyInjection or AnalysisCategories.EfCore))
        {
            return IssueConfidence.High;
        }

        return issue.Category switch
        {
            AnalysisCategories.Architecture => IssueConfidence.High,
            AnalysisCategories.Security when issue.RuleId is "SECSECRET" => IssueConfidence.Low,
            AnalysisCategories.Security => IssueConfidence.Medium,
            AnalysisCategories.ApiReadiness => IssueConfidence.Medium,
            _ => IssueConfidence.Medium
        };
    }

    private static string InferDetectionMethod(AnalysisIssue issue)
    {
        return issue.Category switch
        {
            AnalysisCategories.Architecture => MsBuildProjectConfiguration,
            AnalysisCategories.DependencyInjection when issue.LineNumber is > 0 => RoslynSyntaxAnalysis,
            AnalysisCategories.DependencyInjection => RoslynSyntaxAnalysis,
            AnalysisCategories.EfCore when issue.RuleId is "EF001" => MsBuildProjectConfiguration,
            AnalysisCategories.EfCore when issue.LineNumber is > 0 => RoslynSyntaxAnalysis,
            AnalysisCategories.Security when issue.RuleId is "SECJSON" or "SEC001" => MsBuildProjectConfiguration,
            AnalysisCategories.Security when issue.RuleId is "SECCONN" or "SECSECRET" or "SECJWT" => HeuristicAnalysis,
            AnalysisCategories.Security when issue.LineNumber is > 0 => RoslynSyntaxAnalysis,
            AnalysisCategories.ApiReadiness when issue.LineNumber is > 0 => RoslynSyntaxAnalysis,
            _ => HeuristicAnalysis
        };
    }

    private static string? NormalizeGuidanceDetectionMethod(
        string ruleId,
        AnalysisIssue issue,
        string? guidanceDetectionMethod)
    {
        if (guidanceDetectionMethod != RoslynSemanticAnalysis || IsExplicitSemanticRule(ruleId))
        {
            return guidanceDetectionMethod;
        }

        return issue.LineNumber is > 0 ? RoslynSyntaxAnalysis : HeuristicAnalysis;
    }

    private static bool IsExplicitSemanticRule(string ruleId)
    {
        return ruleId is "DI003";
    }

    private static string GetDefaultWhyItMatters(string category)
    {
        return category switch
        {
            AnalysisCategories.Architecture => "Architecture violations make boundaries harder to enforce and usually increase coupling between business logic, infrastructure, and delivery mechanisms.",
            AnalysisCategories.DependencyInjection => "Dependency injection issues can become runtime startup failures, ambiguous lifetimes, or hard-to-debug behavior.",
            AnalysisCategories.EfCore => "Persistence and migration risks can lead to data loss, brittle deployments, or runtime model failures after release.",
            AnalysisCategories.Security => "Configuration and security gaps can expose secrets, weaken authentication assumptions, or make production APIs reachable in unsafe ways.",
            AnalysisCategories.ApiReadiness => "API readiness gaps make services harder to operate, observe, validate, and recover when they fail under production traffic.",
            _ => "This finding indicates a production-readiness concern that should be reviewed before release."
        };
    }

    private static string GetDefaultDocumentationLabel(string category)
    {
        return category switch
        {
            AnalysisCategories.Architecture => "Microsoft .NET architecture guidance",
            AnalysisCategories.DependencyInjection => "ASP.NET Core dependency injection",
            AnalysisCategories.EfCore => "EF Core documentation",
            AnalysisCategories.Security => "ASP.NET Core security overview",
            AnalysisCategories.ApiReadiness => "ASP.NET Core web API guidance",
            _ => "Microsoft .NET documentation"
        };
    }

    private static string GetDefaultDocumentationHref(string category)
    {
        return category switch
        {
            AnalysisCategories.Architecture => "https://learn.microsoft.com/dotnet/architecture/",
            AnalysisCategories.DependencyInjection => "https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection",
            AnalysisCategories.EfCore => "https://learn.microsoft.com/ef/core/",
            AnalysisCategories.Security => "https://learn.microsoft.com/aspnet/core/security/",
            AnalysisCategories.ApiReadiness => "https://learn.microsoft.com/aspnet/core/web-api/",
            _ => "https://learn.microsoft.com/dotnet/"
        };
    }

    private static AnalysisDocumentationLink CreateLink(string label, string href)
    {
        return new AnalysisDocumentationLink
        {
            Label = label,
            Href = href
        };
    }

    private sealed record IssueGuidance(
        IssueConfidence? Confidence = null,
        string? DetectionMethod = null,
        string? ProblemSummary = null,
        string? WhyDetected = null,
        string? WhyItMatters = null,
        string? RecommendedPattern = null,
        string? SuggestedImplementation = null,
        IReadOnlyList<AnalysisDocumentationLink>? DocumentationLinks = null,
        string? SuggestedSnippet = null,
        string? GoodExample = null,
        string? BadExample = null);
}
