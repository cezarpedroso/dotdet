using Forge.Api.Analysis;
using Forge.Api.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Forge.Api.Analyzers;

public sealed class ApiReadinessAnalyzer
{
    private static readonly HashSet<string> MinimalApiMapMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "MapGet",
        "MapPost",
        "MapPut",
        "MapPatch",
        "MapDelete",
        "MapGroup"
    };

    private readonly SemanticAnalysisHelper _semanticHelper;

    public ApiReadinessAnalyzer(SemanticAnalysisHelper semanticHelper)
    {
        _semanticHelper = semanticHelper;
    }

    public IReadOnlyList<AnalysisIssue> Analyze(SolutionAnalysisContext context)
    {
        var issues = new List<AnalysisIssue>();
        var webProjects = context.Projects
            .Where(AnalyzerUtilities.IsProductionEntryPointProject)
            .ToArray();

        if (webProjects.Length == 0)
        {
            issues.Add(CreateIssue(
                "API001",
                issues.Count,
                "No API/Web project detected",
                "DotDet did not find a project using the Web SDK or ASP.NET Core packages.",
                IssueSeverity.Info,
                null,
                context.SolutionPath,
                null,
                "If this solution exposes HTTP APIs, make sure the API project uses Microsoft.NET.Sdk.Web or ASP.NET Core package references."));
            return issues;
        }

        foreach (var project in webProjects)
        {
            var projectFiles = _semanticHelper.GetSourceFiles(context, project);
            var semanticProjectFiles = _semanticHelper.GetSemanticDocuments(context, project);
            var startupSemanticDocuments = _semanticHelper.GetStartupSemanticDocuments(context, project);
            var startupSourceFiles = _semanticHelper.GetStartupSourceFiles(context, project);
            var allSourceText = string.Join(Environment.NewLine, projectFiles.Select(file => file.Text));
            var startupFile = startupSourceFiles.FirstOrDefault();

            if (!HasController(projectFiles, semanticProjectFiles) && !HasMinimalApi(projectFiles, semanticProjectFiles))
            {
                issues.Add(CreateIssue(
                    "API002",
                    issues.Count,
                    "No controllers or minimal API endpoints found",
                    $"{project.Name} is a web project, but DotDet did not find controllers or MapGet/MapPost-style endpoints.",
                    IssueSeverity.Warning,
                    project,
                    project.FilePath,
                    null,
                    "Add controllers or minimal API route mappings, or exclude non-API host projects from API readiness scoring."));
            }

            if (!HasOpenApiSetup(startupSemanticDocuments, allSourceText))
            {
                issues.Add(CreateIssue(
                    "API003",
                    issues.Count,
                    "Swagger/OpenAPI setup is missing",
                    $"{project.Name} does not appear to configure Swagger or ASP.NET Core OpenAPI.",
                    IssueSeverity.Warning,
                    project,
                    startupFile?.FilePath ?? project.FilePath,
                    startupFile is null ? null : 1,
                    "Add AddOpenApi/MapOpenApi or SwaggerGen/UseSwagger so API consumers can inspect the contract."));
            }

            if (!HasHealthChecks(startupSemanticDocuments, allSourceText))
            {
                issues.Add(CreateIssue(
                    "API004",
                    issues.Count,
                    "Health checks are missing",
                    $"{project.Name} does not appear to register and map ASP.NET Core health checks.",
                    IssueSeverity.Warning,
                    project,
                    startupFile?.FilePath ?? project.FilePath,
                    startupFile is null ? null : 1,
                    "Add AddHealthChecks() and map a health endpoint for orchestrators and uptime probes."));
            }

            if (!HasGlobalExceptionHandling(startupSemanticDocuments, allSourceText))
            {
                issues.Add(CreateIssue(
                    "API005",
                    issues.Count,
                    "Global exception handling is missing",
                    $"{project.Name} does not appear to configure global exception handling or ProblemDetails.",
                    IssueSeverity.Warning,
                    project,
                    startupFile?.FilePath ?? project.FilePath,
                    startupFile is null ? null : 1,
                    "Add UseExceptionHandler, ProblemDetails, or a centralized exception-handling middleware."));
            }

            if (!HasStructuredLogging(startupSemanticDocuments, semanticProjectFiles, allSourceText))
            {
                issues.Add(CreateIssue(
                    "API006",
                    issues.Count,
                    "Structured logging setup is missing",
                    $"{project.Name} does not show Serilog, OpenTelemetry, JSON console logging, or LoggerMessage patterns.",
                    IssueSeverity.Info,
                    project,
                    startupFile?.FilePath ?? project.FilePath,
                    startupFile is null ? null : 1,
                    "Configure structured logging with Serilog, OpenTelemetry, JSON console logging, or source-generated LoggerMessage APIs."));
            }

            if (!HasValidationPattern(project, projectFiles, semanticProjectFiles))
            {
                issues.Add(CreateIssue(
                    "API007",
                    issues.Count,
                    "Request validation pattern is missing",
                    $"{project.Name} does not appear to use FluentValidation, validation attributes, endpoint filters, or IValidatableObject.",
                    IssueSeverity.Info,
                    project,
                    project.FilePath,
                    null,
                    "Add a consistent request-validation pattern so invalid payloads fail predictably before business logic runs."));
            }
        }

        return issues;
    }

    private bool HasController(
        IEnumerable<SourceFileContext> sourceFiles,
        IReadOnlyList<SemanticDocumentContext> semanticDocuments)
    {
        return semanticDocuments.Count > 0
            ? HasSemanticController(semanticDocuments)
            : HasSyntaxController(sourceFiles);
    }

    private bool HasSemanticController(IEnumerable<SemanticDocumentContext> semanticDocuments)
    {
        return semanticDocuments.Any(document => document.Root.DescendantNodes().OfType<ClassDeclarationSyntax>().Any(classDeclaration =>
        {
            var classSymbol = document.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
            return classDeclaration.Identifier.ValueText.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                || _semanticHelper.InheritsFrom(classSymbol, "Microsoft.AspNetCore.Mvc.ControllerBase", "ControllerBase")
                || _semanticHelper.InheritsFrom(classSymbol, "Microsoft.AspNetCore.Mvc.Controller", "Controller")
                || HasControllerAttribute(classSymbol);
        }));
    }

    private static bool HasSyntaxController(IEnumerable<SourceFileContext> sourceFiles)
    {
        return sourceFiles.Any(file => file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>().Any(classDeclaration =>
            classDeclaration.Identifier.ValueText.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            || classDeclaration.BaseList?.Types.Any(type =>
                type.Type.ToString().Contains("ControllerBase", StringComparison.OrdinalIgnoreCase)
                || type.Type.ToString().Contains("Controller", StringComparison.OrdinalIgnoreCase)) == true));
    }

    private bool HasMinimalApi(
        IEnumerable<SourceFileContext> sourceFiles,
        IReadOnlyList<SemanticDocumentContext> semanticDocuments)
    {
        return semanticDocuments.Count > 0
            ? HasSemanticMinimalApi(semanticDocuments)
            : HasSyntaxMinimalApi(sourceFiles);
    }

    private bool HasSemanticMinimalApi(IEnumerable<SemanticDocumentContext> semanticDocuments)
    {
        return semanticDocuments.Any(document => document.Root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
        {
            var methodSymbol = _semanticHelper.GetMethodSymbol(document.SemanticModel, invocation);
            var methodName = methodSymbol?.Name ?? GetInvokedMethodName(invocation);
            var containingNamespace = methodSymbol?.ContainingNamespace?.ToDisplayString() ?? string.Empty;

            return MinimalApiMapMethods.Contains(methodName)
                && (methodSymbol is null
                    || containingNamespace.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                    || methodSymbol.DeclaringSyntaxReferences.Length > 0);
        }));
    }

    private static bool HasSyntaxMinimalApi(IEnumerable<SourceFileContext> sourceFiles)
    {
        return sourceFiles.Any(file => file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
        {
            return MinimalApiMapMethods.Contains(GetInvokedMethodName(invocation));
        }));
    }

    private static bool HasControllerAttribute(INamedTypeSymbol? classSymbol)
    {
        return classSymbol?.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name is "ApiControllerAttribute" or "RouteAttribute"
            || attribute.AttributeClass?.Name.StartsWith("Http", StringComparison.Ordinal) == true) == true;
    }

    private static string GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            _ => string.Empty
        };
    }

    private bool HasOpenApiSetup(IEnumerable<SemanticDocumentContext> startupDocuments, string sourceText)
    {
        if (startupDocuments.Any())
        {
            return _semanticHelper.HasInvocation(startupDocuments, (document, invocation, methodSymbol) =>
            {
                var methodName = methodSymbol?.Name ?? GetInvokedMethodName(invocation);
                return methodName is "AddOpenApi" or "MapOpenApi" or "AddSwaggerGen" or "UseSwagger" or "WithOpenApi";
            });
        }

        return sourceText.Contains("AddOpenApi", StringComparison.Ordinal)
            || sourceText.Contains("MapOpenApi", StringComparison.Ordinal)
            || sourceText.Contains("AddSwaggerGen", StringComparison.Ordinal)
            || sourceText.Contains("UseSwagger", StringComparison.Ordinal)
            || sourceText.Contains("WithOpenApi", StringComparison.Ordinal);
    }

    private bool HasHealthChecks(IEnumerable<SemanticDocumentContext> startupDocuments, string sourceText)
    {
        if (startupDocuments.Any())
        {
            var hasRegistration = _semanticHelper.HasInvocation(startupDocuments, (document, invocation, methodSymbol) =>
                (methodSymbol?.Name ?? GetInvokedMethodName(invocation)).Equals("AddHealthChecks", StringComparison.Ordinal));
            var hasMapping = _semanticHelper.HasInvocation(startupDocuments, (document, invocation, methodSymbol) =>
                (methodSymbol?.Name ?? GetInvokedMethodName(invocation)).Equals("MapHealthChecks", StringComparison.Ordinal));

            return hasRegistration && hasMapping;
        }

        return sourceText.Contains("AddHealthChecks", StringComparison.Ordinal)
            && sourceText.Contains("MapHealthChecks", StringComparison.Ordinal);
    }

    private bool HasGlobalExceptionHandling(IEnumerable<SemanticDocumentContext> startupDocuments, string sourceText)
    {
        if (startupDocuments.Any())
        {
            var hasBuiltInHandling = _semanticHelper.HasInvocation(startupDocuments, (document, invocation, methodSymbol) =>
            {
                var methodName = methodSymbol?.Name ?? GetInvokedMethodName(invocation);
                return methodName is "UseExceptionHandler" or "AddProblemDetails";
            });

            if (hasBuiltInHandling)
            {
                return true;
            }
        }

        return sourceText.Contains("UseExceptionHandler", StringComparison.Ordinal)
            || sourceText.Contains("AddProblemDetails", StringComparison.Ordinal)
            || sourceText.Contains("ProblemDetails", StringComparison.Ordinal)
            || sourceText.Contains("ExceptionHandlingMiddleware", StringComparison.Ordinal);
    }

    private bool HasStructuredLogging(
        IEnumerable<SemanticDocumentContext> startupDocuments,
        IEnumerable<SemanticDocumentContext> projectDocuments,
        string sourceText)
    {
        if (startupDocuments.Any())
        {
            var hasLoggingInvocation = _semanticHelper.HasInvocation(startupDocuments, (document, invocation, methodSymbol) =>
            {
                var methodName = methodSymbol?.Name ?? GetInvokedMethodName(invocation);
                return methodName is "AddJsonConsole" or "AddOpenTelemetry" or "UseSerilog" or "AddSerilog";
            });

            if (hasLoggingInvocation)
            {
                return true;
            }
        }

        if (projectDocuments.Any(document => document.Root.DescendantNodes().OfType<AttributeSyntax>().Any(attribute =>
            document.SemanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol attributeSymbol
            && attributeSymbol.ContainingType.Name is "LoggerMessageAttribute" or "LoggerMessage")))
        {
            return true;
        }

        return sourceText.Contains("Serilog", StringComparison.OrdinalIgnoreCase)
            || sourceText.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase)
            || sourceText.Contains("AddJsonConsole", StringComparison.Ordinal)
            || sourceText.Contains("LoggerMessage", StringComparison.Ordinal);
    }

    private bool HasValidationPattern(
        AnalyzedProject project,
        IEnumerable<SourceFileContext> sourceFiles,
        IEnumerable<SemanticDocumentContext> semanticDocuments)
    {
        if (project.PackageReferences.Any(package => package.Contains("FluentValidation", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (semanticDocuments.Any())
        {
            return semanticDocuments.Any(document =>
                document.Root.DescendantNodes().OfType<ClassDeclarationSyntax>().Any(classDeclaration =>
                    _semanticHelper.ImplementsInterface(
                        document.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol,
                        "System.ComponentModel.DataAnnotations.IValidatableObject"))
                || document.Root.DescendantNodes().OfType<AttributeSyntax>().Any(attribute =>
                    document.SemanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol attributeSymbol
                    && attributeSymbol.ContainingType.ContainingNamespace.ToDisplayString().StartsWith(
                        "System.ComponentModel.DataAnnotations",
                        StringComparison.Ordinal))
                || document.Root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
                {
                    var methodName = _semanticHelper.GetMethodSymbol(document.SemanticModel, invocation)?.Name
                        ?? GetInvokedMethodName(invocation);
                    return methodName is "AddEndpointFilter" or "TryValidateModel";
                }));
        }

        return sourceFiles.Any(file =>
                file.Text.Contains("[Required", StringComparison.Ordinal)
                || file.Text.Contains("[StringLength", StringComparison.Ordinal)
                || file.Text.Contains("[Range", StringComparison.Ordinal)
                || file.Text.Contains("[MinLength", StringComparison.Ordinal)
                || file.Text.Contains("[MaxLength", StringComparison.Ordinal)
                || file.Text.Contains("IValidatableObject", StringComparison.Ordinal)
                || file.Text.Contains("AddEndpointFilter", StringComparison.Ordinal)
                || file.Text.Contains("TryValidateModel", StringComparison.Ordinal));
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
        string recommendation)
    {
        return new AnalysisIssue
        {
            Id = $"{ruleId}-{index + 1:D3}",
            RuleId = ruleId,
            Title = title,
            Description = description,
            Severity = severity,
            Category = AnalysisCategories.ApiReadiness,
            ProjectName = project?.Name,
            FilePath = filePath,
            LineNumber = lineNumber,
            Recommendation = recommendation,
            WhyDetected = AnalyzerUtilities.BuildEvidence(
                ("Rule", ruleId),
                ("Project", project?.Name),
                ("File", filePath),
                ("Line", lineNumber?.ToString()),
                ("Detected", description),
                ("Applicability", project is null
                    ? "Solution-level API readiness signal."
                    : "Production ASP.NET Core entry-point project."))
        };
    }
}
