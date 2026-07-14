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

    private static readonly HashSet<string> ApiIntentInvocationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AddOpenApi",
        "MapOpenApi",
        "AddSwaggerGen",
        "UseSwagger",
        "UseSwaggerUI",
        "WithOpenApi"
    };

    private static readonly HashSet<string> WebUiIntentInvocationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AddRazorPages",
        "MapRazorPages",
        "AddControllersWithViews",
        "MapControllerRoute",
        "AddRazorComponents",
        "MapRazorComponents",
        "AddInteractiveServerComponents",
        "AddInteractiveWebAssemblyComponents",
        "AddServerSideBlazor",
        "MapBlazorHub"
    };

    private static readonly HashSet<string> ControllerRouteInvocationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "MapControllers"
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
            var intent = ClassifyHostIntent(
                context,
                project,
                projectFiles,
                semanticProjectFiles,
                startupSemanticDocuments,
                allSourceText);
            var hasApiIntent = intent is AspNetCoreHostIntent.Api or AspNetCoreHostIntent.MixedApiAndWebUi;
            var hasEndpoint = HasController(projectFiles, semanticProjectFiles) || HasMinimalApi(projectFiles, semanticProjectFiles);
            var shouldRequireOpenApiDocumentation = ShouldRequireOpenApiDocumentation(
                project,
                projectFiles,
                semanticProjectFiles,
                startupSemanticDocuments,
                allSourceText,
                intent);

            if (hasApiIntent && !hasEndpoint)
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
                    "Add controllers or minimal API route mappings, or exclude non-API host projects from API readiness scoring.",
                    intent));
            }

            if (hasApiIntent
                && shouldRequireOpenApiDocumentation
                && !HasOpenApiSetup(startupSemanticDocuments, allSourceText))
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
                    "Add AddOpenApi/MapOpenApi or SwaggerGen/UseSwagger so API consumers can inspect the contract.",
                    intent));
            }

            if (!HasHealthChecks(startupSemanticDocuments, allSourceText))
            {
                var severity = hasApiIntent ? IssueSeverity.Warning : IssueSeverity.Info;
                issues.Add(CreateIssue(
                    "API004",
                    issues.Count,
                    "Health checks are missing",
                    $"{project.Name} does not appear to register and map ASP.NET Core health checks.",
                    severity,
                    project,
                    startupFile?.FilePath ?? project.FilePath,
                    startupFile is null ? null : 1,
                    "Add AddHealthChecks() and map a health endpoint for orchestrators and uptime probes.",
                    intent));
            }

            if (!HasGlobalExceptionHandling(startupSemanticDocuments, allSourceText))
            {
                var severity = hasApiIntent ? IssueSeverity.Warning : IssueSeverity.Info;
                issues.Add(CreateIssue(
                    "API005",
                    issues.Count,
                    "Global exception handling is missing",
                    $"{project.Name} does not appear to configure global exception handling or ProblemDetails.",
                    severity,
                    project,
                    startupFile?.FilePath ?? project.FilePath,
                    startupFile is null ? null : 1,
                    "Add UseExceptionHandler, ProblemDetails, or a centralized exception-handling middleware.",
                    intent));
            }

            if (hasApiIntent && !HasStructuredLogging(startupSemanticDocuments, semanticProjectFiles, allSourceText))
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
                    "Configure structured logging with Serilog, OpenTelemetry, JSON console logging, or source-generated LoggerMessage APIs.",
                    intent));
            }

            if (hasApiIntent && !HasValidationPattern(project, projectFiles, semanticProjectFiles))
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
                    "Add a consistent request-validation pattern so invalid payloads fail predictably before business logic runs.",
                    intent));
            }
        }

        return issues;
    }

    private AspNetCoreHostIntent ClassifyHostIntent(
        SolutionAnalysisContext context,
        AnalyzedProject project,
        IReadOnlyList<SourceFileContext> sourceFiles,
        IReadOnlyList<SemanticDocumentContext> semanticDocuments,
        IReadOnlyList<SemanticDocumentContext> startupDocuments,
        string sourceText)
    {
        if (!AnalyzerUtilities.IsProductionEntryPointProject(project))
        {
            return AspNetCoreHostIntent.NonWeb;
        }

        var webUiIntent = HasWebUiIntent(project, startupDocuments, sourceText);
        var apiIntent = HasApiIntent(project, sourceFiles, semanticDocuments, startupDocuments, sourceText, webUiIntent);

        return (apiIntent, webUiIntent) switch
        {
            (true, true) => AspNetCoreHostIntent.MixedApiAndWebUi,
            (true, false) => AspNetCoreHostIntent.Api,
            (false, true) => AspNetCoreHostIntent.WebUi,
            _ => AspNetCoreHostIntent.UnknownWebHost
        };
    }

    private bool HasApiIntent(
        AnalyzedProject project,
        IReadOnlyList<SourceFileContext> sourceFiles,
        IReadOnlyList<SemanticDocumentContext> semanticDocuments,
        IReadOnlyList<SemanticDocumentContext> startupDocuments,
        string sourceText,
        bool webUiIntent)
    {
        var hasApiName = HasApiProjectName(project);
        var hasApiController = HasApiController(sourceFiles, semanticDocuments);
        var hasMinimalApi = HasMinimalApi(sourceFiles, semanticDocuments);
        var hasOpenApiSetup = HasInvocationNamed(startupDocuments, sourceText, ApiIntentInvocationNames);
        var hasApiRouting = !webUiIntent && HasInvocationNamed(startupDocuments, sourceText, ControllerRouteInvocationNames);
        var hasResultEndpoint = HasResultEndpointSignal(sourceFiles, semanticDocuments, sourceText);

        return hasApiName
            || hasApiController
            || hasMinimalApi
            || hasOpenApiSetup
            || hasApiRouting
            || hasResultEndpoint
            || (!webUiIntent && HasApiPackageReference(project));
    }

    private bool ShouldRequireOpenApiDocumentation(
        AnalyzedProject project,
        IReadOnlyList<SourceFileContext> sourceFiles,
        IReadOnlyList<SemanticDocumentContext> semanticDocuments,
        IReadOnlyList<SemanticDocumentContext> startupDocuments,
        string sourceText,
        AspNetCoreHostIntent intent)
    {
        if (intent == AspNetCoreHostIntent.Api)
        {
            return true;
        }

        if (intent != AspNetCoreHostIntent.MixedApiAndWebUi)
        {
            return false;
        }

        return HasApiProjectName(project)
            || HasInvocationNamed(startupDocuments, sourceText, ControllerRouteInvocationNames)
            || HasMinimalApi(sourceFiles, semanticDocuments)
            || HasConcreteApiActionRoute(sourceFiles);
    }

    private static bool HasApiProjectName(AnalyzedProject project)
    {
        return AnalyzerUtilities.HasToken(project.Name, "Api")
            || AnalyzerUtilities.HasToken(project.Name, "PublicApi");
    }

    private static bool HasApiPackageReference(AnalyzedProject project)
    {
        return project.PackageReferences.Any(package =>
            package.Contains("OpenApi", StringComparison.OrdinalIgnoreCase)
            || package.Contains("Swagger", StringComparison.OrdinalIgnoreCase)
            || package.Contains("NSwag", StringComparison.OrdinalIgnoreCase));
    }

    private bool HasWebUiIntent(
        AnalyzedProject project,
        IReadOnlyList<SemanticDocumentContext> startupDocuments,
        string sourceText)
    {
        return AnalyzerUtilities.HasToken(project.Name, "Web")
            || AnalyzerUtilities.HasToken(project.Name, "Ui")
            || AnalyzerUtilities.HasToken(project.Name, "Mvc")
            || AnalyzerUtilities.HasToken(project.Name, "Razor")
            || AnalyzerUtilities.HasToken(project.Name, "Blazor")
            || project.PackageReferences.Any(package =>
                package.Contains("Razor", StringComparison.OrdinalIgnoreCase)
                || package.Contains("Blazor", StringComparison.OrdinalIgnoreCase)
                || package.Contains("Microsoft.AspNetCore.Components", StringComparison.OrdinalIgnoreCase))
            || HasWebUiFilesOrFolders(project)
            || HasInvocationNamed(startupDocuments, sourceText, WebUiIntentInvocationNames);
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

    private bool HasApiController(
        IEnumerable<SourceFileContext> sourceFiles,
        IReadOnlyList<SemanticDocumentContext> semanticDocuments)
    {
        return semanticDocuments.Count > 0
            ? semanticDocuments.Any(document => document.Root.DescendantNodes().OfType<ClassDeclarationSyntax>().Any(classDeclaration =>
            {
                var classSymbol = document.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                var inheritsMvcController = _semanticHelper.InheritsFrom(classSymbol, "Microsoft.AspNetCore.Mvc.Controller", "Controller");
                return HasApiControllerAttribute(classSymbol)
                    || (!inheritsMvcController
                        && _semanticHelper.InheritsFrom(classSymbol, "Microsoft.AspNetCore.Mvc.ControllerBase", "ControllerBase"));
            }))
            : sourceFiles.Any(file => file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>().Any(classDeclaration =>
                classDeclaration.BaseList?.Types.Any(type =>
                    type.Type.ToString().Contains("ControllerBase", StringComparison.OrdinalIgnoreCase)) == true
                || classDeclaration.AttributeLists
                    .SelectMany(attributeList => attributeList.Attributes)
                    .Any(attribute => IsAttributeNamed(attribute, "ApiController"))));
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

    private static bool HasConcreteApiActionRoute(IEnumerable<SourceFileContext> sourceFiles)
    {
        return sourceFiles.Any(file => file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>().Any(classDeclaration =>
        {
            var classRouteStartsWithApi = classDeclaration.AttributeLists
                .SelectMany(attributeList => attributeList.Attributes)
                .Where(IsRouteAttribute)
                .Select(GetRouteTemplate)
                .Any(IsApiRouteTemplate);

            return classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(methodDeclaration =>
                {
                    var actionAttributes = methodDeclaration.AttributeLists
                        .SelectMany(attributeList => attributeList.Attributes)
                        .Where(attribute => IsRouteAttribute(attribute) || IsHttpMethodAttribute(attribute))
                        .ToArray();

                    return actionAttributes.Any(attribute => IsApiRouteTemplate(GetRouteTemplate(attribute)))
                        || (classRouteStartsWithApi && actionAttributes.Length > 0);
                });
        }));
    }

    private static bool HasControllerAttribute(INamedTypeSymbol? classSymbol)
    {
        return classSymbol?.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name is "ApiControllerAttribute" or "RouteAttribute"
            || attribute.AttributeClass?.Name.StartsWith("Http", StringComparison.Ordinal) == true) == true;
    }

    private static bool HasApiControllerAttribute(INamedTypeSymbol? classSymbol)
    {
        return classSymbol?.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name is "ApiControllerAttribute") == true;
    }

    private static bool IsAttributeNamed(AttributeSyntax attribute, string name)
    {
        var attributeName = attribute.Name.ToString();
        return attributeName.Equals(name, StringComparison.OrdinalIgnoreCase)
            || attributeName.Equals($"{name}Attribute", StringComparison.OrdinalIgnoreCase)
            || attributeName.EndsWith($".{name}", StringComparison.OrdinalIgnoreCase)
            || attributeName.EndsWith($".{name}Attribute", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRouteAttribute(AttributeSyntax attribute)
    {
        return IsAttributeNamed(attribute, "Route");
    }

    private static bool IsHttpMethodAttribute(AttributeSyntax attribute)
    {
        return IsAttributeNamed(attribute, "HttpGet")
            || IsAttributeNamed(attribute, "HttpPost")
            || IsAttributeNamed(attribute, "HttpPut")
            || IsAttributeNamed(attribute, "HttpDelete")
            || IsAttributeNamed(attribute, "HttpPatch")
            || IsAttributeNamed(attribute, "HttpHead")
            || IsAttributeNamed(attribute, "HttpOptions");
    }

    private static string? GetRouteTemplate(AttributeSyntax attribute)
    {
        return attribute.ArgumentList?.Arguments
            .Select(argument => argument.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText)
            .FirstOrDefault();
    }

    private static bool IsApiRouteTemplate(string? routeTemplate)
    {
        if (string.IsNullOrWhiteSpace(routeTemplate))
        {
            return false;
        }

        var normalized = routeTemplate.Trim().TrimStart('~').TrimStart('/');
        return normalized.Equals("api", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("api/", StringComparison.OrdinalIgnoreCase);
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

    private bool HasInvocationNamed(
        IEnumerable<SemanticDocumentContext> startupDocuments,
        string sourceText,
        IReadOnlySet<string> methodNames)
    {
        if (startupDocuments.Any())
        {
            return _semanticHelper.HasInvocation(startupDocuments, (document, invocation, methodSymbol) =>
                methodNames.Contains(methodSymbol?.Name ?? GetInvokedMethodName(invocation)));
        }

        return methodNames.Any(methodName => sourceText.Contains(methodName, StringComparison.Ordinal));
    }

    private bool HasResultEndpointSignal(
        IReadOnlyList<SourceFileContext> sourceFiles,
        IReadOnlyList<SemanticDocumentContext> semanticDocuments,
        string sourceText)
    {
        if (semanticDocuments.Count > 0)
        {
            var hasIResultType = semanticDocuments.Any(document =>
                document.Root.DescendantNodes().OfType<TypeSyntax>().Any(typeSyntax =>
                {
                    var typeSymbol = document.SemanticModel.GetTypeInfo(typeSyntax).Type;
                    return _semanticHelper.IsNamedOrConstructedFrom(
                        typeSymbol,
                        "Microsoft.AspNetCore.Http.IResult",
                        "IResult");
                }));

            if (hasIResultType)
            {
                return true;
            }
        }

        return sourceFiles.Any(file =>
                file.Text.Contains("IResult", StringComparison.Ordinal)
                || file.Text.Contains("Results.", StringComparison.Ordinal)
                || file.Text.Contains("TypedResults.", StringComparison.Ordinal))
            || sourceText.Contains("IResult", StringComparison.Ordinal)
            || sourceText.Contains("Results.", StringComparison.Ordinal)
            || sourceText.Contains("TypedResults.", StringComparison.Ordinal);
    }

    private static bool HasWebUiFilesOrFolders(AnalyzedProject project)
    {
        try
        {
            if (Directory.Exists(Path.Combine(project.DirectoryPath, "Pages"))
                || Directory.Exists(Path.Combine(project.DirectoryPath, "Views"))
                || Directory.Exists(Path.Combine(project.DirectoryPath, "wwwroot")))
            {
                return true;
            }

            return Directory.EnumerateFiles(project.DirectoryPath, "*.*", SearchOption.AllDirectories)
                .Where(path => !AnalyzerUtilities.IsUnderBuildOutput(path))
                .Any(path =>
                    path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return false;
        }
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
        string recommendation,
        AspNetCoreHostIntent? intent = null)
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
                ("Project intent", intent?.ToString()),
                ("Detected", description),
                ("Applicability", project is null
                    ? "Solution-level API readiness signal."
                    : GetApplicabilityDescription(intent ?? AspNetCoreHostIntent.UnknownWebHost)))
        };
    }

    private static string GetApplicabilityDescription(AspNetCoreHostIntent intent)
    {
        return intent switch
        {
            AspNetCoreHostIntent.Api => "API readiness checks apply because API intent was detected.",
            AspNetCoreHostIntent.MixedApiAndWebUi => "API readiness checks apply because the host contains both API and Web UI signals.",
            AspNetCoreHostIntent.WebUi => "API-specific checks are not applicable; only low-impact operational web-host guidance applies.",
            AspNetCoreHostIntent.UnknownWebHost => "API intent was not detected; API-specific checks are suppressed.",
            AspNetCoreHostIntent.NonWeb => "Not an ASP.NET Core web host.",
            _ => "Production ASP.NET Core entry-point project."
        };
    }

    private enum AspNetCoreHostIntent
    {
        Api,
        WebUi,
        MixedApiAndWebUi,
        UnknownWebHost,
        NonWeb
    }
}
