using System.Text.Json;
using Forge.Api.Analysis;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Forge.Api.Analyzers;

public sealed class SecurityConfigurationAnalyzer
{
    private readonly SemanticAnalysisHelper _semanticHelper;

    public SecurityConfigurationAnalyzer(SemanticAnalysisHelper semanticHelper)
    {
        _semanticHelper = semanticHelper;
    }

    public IReadOnlyList<AnalysisIssue> Analyze(SolutionAnalysisContext context)
    {
        var issues = new List<AnalysisIssue>();
        var webProjects = context.Projects
            .Where(AnalyzerUtilities.IsProductionEntryPointProject)
            .ToArray();

        if (webProjects.Length > 0 && context.AppSettingsFiles.Count == 0)
        {
            issues.Add(CreateIssue(
                "SEC001",
                issues.Count,
                "No appsettings.json files found",
                "DotDet did not find appsettings.json or environment-specific appsettings files for the web/API projects.",
                IssueSeverity.Warning,
                webProjects[0],
                webProjects[0].FilePath,
                null,
                "Add explicit appsettings files or document how configuration is supplied in each environment."));
        }

        foreach (var configIssue in AnalyzeConfigurationFiles(context))
        {
            issues.Add(configIssue with { Id = $"{configIssue.RuleId ?? "SEC"}-{issues.Count + 1:D3}" });
        }

        foreach (var project in webProjects)
        {
            var startupSemanticDocuments = _semanticHelper.GetStartupSemanticDocuments(context, project);
            var startupSourceFiles = _semanticHelper.GetStartupSourceFiles(context, project);
            var startupFile = startupSourceFiles.FirstOrDefault();
            var startupText = string.Join(Environment.NewLine, startupSourceFiles.Select(file => file.Text));
            var hasProtectedEndpoints = HasProtectedEndpointUsage(startupSemanticDocuments, startupSourceFiles, context, project);

            var insecureCorsInvocation = startupSemanticDocuments.Count > 0
                ? _semanticHelper.FindFirstInvocation(startupSemanticDocuments, IsAllowAnyOriginInvocation)
                : FindSyntaxInvocation(startupSourceFiles, "AllowAnyOrigin");

            if (insecureCorsInvocation is not null)
            {
                issues.Add(CreateIssue(
                    "SEC002",
                    issues.Count,
                    "CORS policy allows any origin",
                    hasProtectedEndpoints
                        ? $"{Path.GetFileName(insecureCorsInvocation.FilePath)} calls AllowAnyOrigin and the project appears to define protected endpoints or authorization policies."
                        : $"{Path.GetFileName(insecureCorsInvocation.FilePath)} calls AllowAnyOrigin. DotDet did not find protected endpoints, so this is review-level CORS exposure rather than a confirmed release blocker.",
                    hasProtectedEndpoints ? IssueSeverity.Error : IssueSeverity.Warning,
                    project,
                    insecureCorsInvocation.FilePath,
                    insecureCorsInvocation.LineNumber,
                    "Restrict CORS origins by environment and avoid AllowAnyOrigin for authenticated or sensitive APIs.",
                    IssueConfidence.High,
                    IssueEnrichmentService.RoslynSemanticAnalysis));
            }

            var hasHttpsRedirection = startupSemanticDocuments.Count > 0
                ? _semanticHelper.HasInvocation(startupSemanticDocuments, (document, invocation, methodSymbol) =>
                    GetMethodName(methodSymbol, invocation).Equals("UseHttpsRedirection", StringComparison.Ordinal))
                : startupText.Contains("UseHttpsRedirection", StringComparison.Ordinal);

            if (!hasHttpsRedirection)
            {
                issues.Add(CreateIssue(
                    "SEC003",
                    issues.Count,
                    "HTTPS redirection middleware is missing",
                    $"{project.Name} does not appear to call UseHttpsRedirection.",
                    IssueSeverity.Warning,
                    project,
                    startupFile?.FilePath ?? project.FilePath,
                    startupFile is null ? null : 1,
                    "Call app.UseHttpsRedirection() for browser/API workloads unless TLS is fully terminated and enforced upstream."));
            }

            if (!HasJwtPackage(project))
            {
                continue;
            }

            var hasAuthUsage = HasAuthenticationUsage(startupSemanticDocuments, startupSourceFiles, context, project);
            var hasAuthentication = startupSemanticDocuments.Count > 0
                ? _semanticHelper.HasInvocation(startupSemanticDocuments, (document, invocation, methodSymbol) =>
                    GetMethodName(methodSymbol, invocation).Equals("UseAuthentication", StringComparison.Ordinal))
                : startupText.Contains("UseAuthentication", StringComparison.Ordinal);

            if (!hasAuthentication)
            {
                var severity = hasAuthUsage ? IssueSeverity.Error : IssueSeverity.Info;
                issues.Add(CreateIssue(
                    "SEC004",
                    issues.Count,
                    "JWT package present without authentication middleware",
                    hasAuthUsage
                        ? $"{project.Name} references JWT authentication packages and appears to define protected endpoints or policies, but does not appear to call UseAuthentication."
                        : $"{project.Name} references JWT authentication packages but DotDet did not find protected endpoints or policies that confirm authentication is active.",
                    severity,
                    project,
                    startupFile?.FilePath ?? project.FilePath,
                    startupFile is null ? null : 1,
                    hasAuthUsage
                        ? "Add app.UseAuthentication() before app.UseAuthorization() in the request pipeline."
                        : "If this application does not expose authenticated endpoints, remove the unused JWT package or document why authentication is configured elsewhere.",
                    hasAuthUsage ? IssueConfidence.High : IssueConfidence.Low,
                    startupSemanticDocuments.Count > 0 ? IssueEnrichmentService.RoslynSemanticAnalysis : IssueEnrichmentService.RoslynSyntaxAnalysis));
            }

            var hasAuthorization = startupSemanticDocuments.Count > 0
                ? _semanticHelper.HasInvocation(startupSemanticDocuments, (document, invocation, methodSymbol) =>
                    GetMethodName(methodSymbol, invocation).Equals("UseAuthorization", StringComparison.Ordinal))
                : startupText.Contains("UseAuthorization", StringComparison.Ordinal);

            if (!hasAuthorization)
            {
                issues.Add(CreateIssue(
                    "SEC005",
                    issues.Count,
                    "JWT package present without authorization middleware",
                    hasProtectedEndpoints
                        ? $"{project.Name} appears to define protected endpoints or policies but does not appear to call UseAuthorization."
                        : $"{project.Name} references JWT authentication packages but DotDet did not find protected endpoints or policies that confirm authorization is active.",
                    hasProtectedEndpoints ? IssueSeverity.Warning : IssueSeverity.Info,
                    project,
                    startupFile?.FilePath ?? project.FilePath,
                    startupFile is null ? null : 1,
                    "Add app.UseAuthorization() and protect endpoints with policies or authorization attributes.",
                    hasProtectedEndpoints ? IssueConfidence.Medium : IssueConfidence.Low,
                    startupSemanticDocuments.Count > 0 ? IssueEnrichmentService.RoslynSemanticAnalysis : IssueEnrichmentService.RoslynSyntaxAnalysis));
            }
        }

        return issues;
    }

    private static bool IsAllowAnyOriginInvocation(
        SemanticDocumentContext document,
        InvocationExpressionSyntax invocation,
        IMethodSymbol? methodSymbol)
    {
        var methodName = GetMethodName(methodSymbol, invocation);
        if (!methodName.Equals("AllowAnyOrigin", StringComparison.Ordinal))
        {
            return false;
        }

        var containingNamespace = methodSymbol?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return methodSymbol is null
            || containingNamespace.StartsWith("Microsoft.AspNetCore.Cors", StringComparison.Ordinal)
            || containingNamespace.StartsWith("Microsoft.AspNetCore.Builder", StringComparison.Ordinal);
    }

    private static InvocationMatch? FindSyntaxInvocation(IEnumerable<SourceFileContext> sourceFiles, string methodName)
    {
        foreach (var sourceFile in sourceFiles)
        {
            foreach (var invocation in sourceFile.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!GetMethodName(null, invocation).Equals(methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                return new InvocationMatch(
                    sourceFile.Project,
                    sourceFile.FilePath,
                    AnalyzerUtilities.GetLineNumber(invocation),
                    invocation,
                    null);
            }
        }

        return null;
    }

    private static IEnumerable<AnalysisIssue> AnalyzeConfigurationFiles(SolutionAnalysisContext context)
    {
        foreach (var file in context.AppSettingsFiles)
        {
            var owningProject = FindOwningProject(context, file);
            if (owningProject?.IsTestProject == true)
            {
                continue;
            }

            var text = File.ReadAllText(file);
            JsonDocument? document = null;
            AnalysisIssue? parseIssue = null;

            try
            {
                document = JsonDocument.Parse(text);
            }
            catch (JsonException)
            {
                parseIssue = CreateIssue(
                    "SECJSON",
                    0,
                    "Configuration file is not valid JSON",
                    $"{Path.GetFileName(file)} could not be parsed as JSON.",
                    IssueSeverity.Warning,
                    owningProject,
                    file,
                    1,
                    "Fix JSON syntax so configuration can be validated by tooling.");
            }

            if (parseIssue is not null)
            {
                yield return parseIssue;
                continue;
            }

            var parsedDocument = document!;
            using (parsedDocument)
            {
                foreach (var value in Flatten(parsedDocument.RootElement))
                {
                    var propertyName = value.Path.Split(':').Last();
                    var lineNumber = FindJsonPropertyLine(text, propertyName);

                    if (value.Path.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase)
                        && !IsPlaceholder(value.Value))
                    {
                        var severity = GetConnectionStringSeverity(file, value.Value);
                        if (severity is null)
                        {
                            continue;
                        }

                        yield return CreateIssue(
                            "SECCONN",
                            0,
                            "Connection string is stored in configuration",
                            $"{Path.GetFileName(file)} contains a connection string value for {value.Path}.",
                            severity.Value,
                            owningProject,
                            file,
                            lineNumber,
                            severity.Value == IssueSeverity.Error
                                ? "Move credential-bearing production connection strings to user secrets, environment variables, or a managed secret store."
                                : "Keep non-secret local connection strings documented and supply production values through deployment configuration.");
                    }

                    if (value.Path.Contains("Jwt", StringComparison.OrdinalIgnoreCase)
                        && IsJwtSetting(propertyName)
                        && IsWeakJwtValue(propertyName, value.Value))
                    {
                        if (owningProject is not null && !AnalyzerUtilities.IsProductionEntryPointProject(owningProject))
                        {
                            continue;
                        }

                        var severity = GetJwtSettingSeverity(file, propertyName, value.Value);
                        if (severity is null)
                        {
                            continue;
                        }

                        yield return CreateIssue(
                            "SECJWT",
                            0,
                            "Weak JWT configuration value",
                            $"{Path.GetFileName(file)} has a weak or empty JWT {propertyName} value.",
                            severity.Value,
                            owningProject,
                            file,
                            lineNumber,
                            "Set non-empty issuer/audience values and store a high-entropy signing key outside committed appsettings files.",
                            severity.Value == IssueSeverity.Error ? IssueConfidence.Medium : IssueConfidence.Low,
                            IssueEnrichmentService.HeuristicAnalysis);
                    }
                    else if (IsSensitiveKey(propertyName) && !IsPlaceholder(value.Value))
                    {
                        var severity = GetSensitiveValueSeverity(file, value.Value);
                        if (severity is null)
                        {
                            continue;
                        }

                        yield return CreateIssue(
                            "SECSECRET",
                            0,
                            "Possible secret stored in configuration",
                            $"{Path.GetFileName(file)} contains a non-empty value for {value.Path}.",
                            severity.Value,
                            owningProject,
                            file,
                            lineNumber,
                            "Keep secrets out of source-controlled configuration and load them from user secrets, environment variables, or a secret manager.",
                            IssueConfidence.Low,
                            IssueEnrichmentService.HeuristicAnalysis);
                    }
                }
            }
        }
    }

    private static IEnumerable<ConfigValue> Flatten(JsonElement element, string prefix = "")
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var path = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}:{property.Name}";
                foreach (var child in Flatten(property.Value, path))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            yield return new ConfigValue(prefix, element.GetString() ?? string.Empty);
        }
    }

    private static int? FindJsonPropertyLine(string text, string propertyName)
    {
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Contains($"\"{propertyName}\"", StringComparison.OrdinalIgnoreCase))
            {
                return index + 1;
            }
        }

        return null;
    }

    private static bool IsPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            || value.Contains("changeme", StringComparison.OrdinalIgnoreCase)
            || value.Contains("change-me", StringComparison.OrdinalIgnoreCase)
            || value.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
            || value.Contains("your-", StringComparison.OrdinalIgnoreCase)
            || value.Contains("<", StringComparison.Ordinal);
    }

    private static IssueSeverity? GetConnectionStringSeverity(string filePath, string value)
    {
        if (IsLikelyLocalDevelopmentConnection(value))
        {
            return ContainsLikelySecret(value) ? IssueSeverity.Info : null;
        }

        if (ContainsLikelySecret(value) && IsProductionLikeConfigurationFile(filePath) && ContainsProductionLookingEndpoint(value))
        {
            return IssueSeverity.Error;
        }

        if (ContainsLikelySecret(value))
        {
            return IssueSeverity.Warning;
        }

        if (IsLikelyLocalDevelopmentConnection(value) || IsNonProductionConfigurationFile(filePath))
        {
            return null;
        }

        return IssueSeverity.Info;
    }

    private static IssueSeverity? GetJwtSettingSeverity(string filePath, string propertyName, string value)
    {
        var isProductionLike = IsProductionLikeConfigurationFile(filePath);
        var isSigningKey = propertyName.Equals("Key", StringComparison.OrdinalIgnoreCase);

        if (!isProductionLike)
        {
            return IsPlaceholder(value) ? null : IssueSeverity.Info;
        }

        if (isSigningKey && !IsPlaceholder(value) && value.Length < 32)
        {
            return IssueSeverity.Error;
        }

        return IssueSeverity.Warning;
    }

    private static IssueSeverity? GetSensitiveValueSeverity(string filePath, string value)
    {
        if (IsPlaceholder(value))
        {
            return null;
        }

        if (IsNonProductionConfigurationFile(filePath) && !LooksLikeLiveCredential(value))
        {
            return IssueSeverity.Info;
        }

        return LooksLikeLiveCredential(value) || IsProductionLikeConfigurationFile(filePath)
            ? IssueSeverity.Warning
            : IssueSeverity.Info;
    }

    private static bool LooksLikeLiveCredential(string value)
    {
        return value.Contains("sk_live", StringComparison.OrdinalIgnoreCase)
            || value.Contains("pk_live", StringComparison.OrdinalIgnoreCase)
            || value.Contains("BEGIN PRIVATE KEY", StringComparison.OrdinalIgnoreCase)
            || value.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("SharedAccessKey=", StringComparison.OrdinalIgnoreCase)
            || value.Length >= 32;
    }

    private static bool ContainsLikelySecret(string value)
    {
        return value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Pwd=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("User Id=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("UserID=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("SharedAccessKey=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("AccessKey=", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsProductionLookingEndpoint(string value)
    {
        return value.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".postgres.database.azure.com", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".mysql.database.azure.com", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".redis.cache.windows.net", StringComparison.OrdinalIgnoreCase)
            || value.Contains("amazonaws.com", StringComparison.OrdinalIgnoreCase)
            || value.Contains("prod", StringComparison.OrdinalIgnoreCase)
            || value.Contains("production", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProductionLikeConfigurationFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Production", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Prod", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonProductionConfigurationFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return fileName.Contains("Development", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Docker", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Local", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Sample", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Test", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyLocalDevelopmentConnection(string value)
    {
        return value.Contains("(localdb)", StringComparison.OrdinalIgnoreCase)
            || value.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || value.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || value.Contains("DataSource=:memory:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Data Source=:memory:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("UseInMemoryDatabase", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Trusted_Connection=True", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Integrated Security=True", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSensitiveKey(string propertyName)
    {
        return propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
            || propertyName.EndsWith("PrivateKey", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJwtSetting(string propertyName)
    {
        return propertyName.Equals("Issuer", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("Audience", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("Key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWeakJwtValue(string propertyName, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsPlaceholder(value))
        {
            return true;
        }

        return propertyName.Equals("Key", StringComparison.OrdinalIgnoreCase) && value.Length < 32;
    }

    private static bool HasJwtPackage(AnalyzedProject project)
    {
        return project.PackageReferences.Any(package =>
            package.Contains("JwtBearer", StringComparison.OrdinalIgnoreCase)
            || package.Contains("System.IdentityModel.Tokens.Jwt", StringComparison.OrdinalIgnoreCase));
    }

    private bool HasAuthenticationUsage(
        IReadOnlyList<SemanticDocumentContext> startupSemanticDocuments,
        IReadOnlyList<SourceFileContext> startupSourceFiles,
        SolutionAnalysisContext context,
        AnalyzedProject project)
    {
        var projectSemanticDocuments = _semanticHelper.GetSemanticDocuments(context, project);
        var projectSourceFiles = _semanticHelper.GetSourceFiles(context, project);

        if (startupSemanticDocuments.Count > 0 || projectSemanticDocuments.Count > 0)
        {
            var semanticDocuments = startupSemanticDocuments.Concat(projectSemanticDocuments).ToArray();
            return _semanticHelper.HasInvocation(semanticDocuments, (document, invocation, methodSymbol) =>
                GetMethodName(methodSymbol, invocation) is "AddAuthentication" or "AddAuthorization" or "AddJwtBearer")
                || HasAuthorizeAttribute(semanticDocuments)
                || _semanticHelper.HasInvocation(semanticDocuments, (document, invocation, methodSymbol) =>
                    GetMethodName(methodSymbol, invocation).Equals("RequireAuthorization", StringComparison.Ordinal));
        }

        var sourceText = string.Join(Environment.NewLine, startupSourceFiles.Concat(projectSourceFiles).Select(file => file.Text));
        return sourceText.Contains("AddAuthentication", StringComparison.Ordinal)
            || sourceText.Contains("AddAuthorization", StringComparison.Ordinal)
            || sourceText.Contains("AddJwtBearer", StringComparison.Ordinal)
            || sourceText.Contains("[Authorize", StringComparison.Ordinal)
            || sourceText.Contains(".RequireAuthorization", StringComparison.Ordinal);
    }

    private bool HasProtectedEndpointUsage(
        IReadOnlyList<SemanticDocumentContext> startupSemanticDocuments,
        IReadOnlyList<SourceFileContext> startupSourceFiles,
        SolutionAnalysisContext context,
        AnalyzedProject project)
    {
        var projectSemanticDocuments = _semanticHelper.GetSemanticDocuments(context, project);
        var projectSourceFiles = _semanticHelper.GetSourceFiles(context, project);

        if (startupSemanticDocuments.Count > 0 || projectSemanticDocuments.Count > 0)
        {
            var semanticDocuments = startupSemanticDocuments.Concat(projectSemanticDocuments).ToArray();
            return _semanticHelper.HasInvocation(semanticDocuments, (document, invocation, methodSymbol) =>
                    GetMethodName(methodSymbol, invocation) is "AddAuthorization" or "RequireAuthorization")
                || HasAuthorizeAttribute(semanticDocuments);
        }

        var sourceText = string.Join(Environment.NewLine, startupSourceFiles.Concat(projectSourceFiles).Select(file => file.Text));
        return sourceText.Contains("AddAuthorization", StringComparison.Ordinal)
            || sourceText.Contains("[Authorize", StringComparison.Ordinal)
            || sourceText.Contains(".RequireAuthorization", StringComparison.Ordinal);
    }

    private static bool HasAuthorizeAttribute(IEnumerable<SemanticDocumentContext> semanticDocuments)
    {
        return semanticDocuments.Any(document => document.Root.DescendantNodes().OfType<AttributeSyntax>().Any(attribute =>
        {
            var attributeName = (document.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol)?.ContainingType.Name
                ?? attribute.Name.ToString();
            return attributeName is "AuthorizeAttribute" or "Authorize"
                || attributeName.Contains("Authorize", StringComparison.OrdinalIgnoreCase);
        }));
    }

    private static AnalyzedProject? FindOwningProject(SolutionAnalysisContext context, string filePath)
    {
        return context.Projects
            .OrderByDescending(project => project.DirectoryPath.Length)
            .FirstOrDefault(project => filePath.StartsWith(project.DirectoryPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetMethodName(IMethodSymbol? methodSymbol, InvocationExpressionSyntax invocation)
    {
        if (methodSymbol?.Name is { Length: > 0 } symbolName)
        {
            return symbolName;
        }

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            _ => string.Empty
        };
    }

    private static AnalysisIssue CreateIssue(
        string ruleId,
        int index,
        string title,
        string description,
        IssueSeverity severity,
        AnalyzedProject? project,
        string? filePath,
        int? lineNumber,
        string recommendation,
        IssueConfidence? confidence = null,
        string? detectionMethod = null)
    {
        return new AnalysisIssue
        {
            Id = $"{ruleId}-{index + 1:D3}",
            RuleId = ruleId,
            Title = title,
            Description = description,
            Severity = severity,
            Category = AnalysisCategories.Security,
            ProjectName = project?.Name,
            FilePath = filePath,
            LineNumber = lineNumber,
            Recommendation = recommendation,
            Confidence = confidence,
            DetectionMethod = detectionMethod,
            WhyDetected = AnalyzerUtilities.BuildEvidence(
                ("Rule", ruleId),
                ("Project", project?.Name),
                ("File", filePath),
                ("Line", lineNumber?.ToString()),
                ("Detected", description),
                ("Applicability", project is null
                    ? "Configuration file included in the analyzed solution."
                    : AnalyzerUtilities.IsProductionEntryPointProject(project)
                        ? "Production ASP.NET Core entry-point project."
                        : "Production configuration file owned by this project."))
        };
    }

    private sealed record ConfigValue(string Path, string Value);
}
