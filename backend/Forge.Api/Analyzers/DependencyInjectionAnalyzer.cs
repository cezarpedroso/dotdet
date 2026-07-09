using Forge.Api.Analysis;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Forge.Api.Analyzers;

public sealed class DependencyInjectionAnalyzer
{
    private const int MaxCompositionDepth = 4;

    private static readonly HashSet<string> RegistrationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "AddScoped",
        "AddSingleton",
        "AddTransient",
        "TryAddScoped",
        "TryAddSingleton",
        "TryAddTransient",
        "AddDbContext",
        "AddDbContextPool",
        "AddPooledDbContextFactory",
        "AddHostedService",
        "AddHttpClient"
    };

    private static readonly HashSet<string> ScopedRegistrationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "AddScoped",
        "TryAddScoped",
        "AddDbContext",
        "AddDbContextPool",
        "AddPooledDbContextFactory"
    };

    private static readonly HashSet<string> SingletonRegistrationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "AddSingleton",
        "TryAddSingleton",
        "AddHostedService"
    };

    public IReadOnlyList<AnalysisIssue> Analyze(SolutionAnalysisContext context)
    {
        var issues = new List<AnalysisIssue>();
        var productionContext = context with
        {
            SourceFiles = context.SourceFiles.Where(file => AnalyzerUtilities.IsProductionProject(file.Project)).ToArray(),
            SemanticDocuments = context.SemanticDocuments.Where(document => AnalyzerUtilities.IsProductionProject(document.Project)).ToArray(),
            SemanticProjects = context.SemanticProjects.Where(project => AnalyzerUtilities.IsProductionProject(project.Project)).ToArray()
        };
        var registrations = context.SemanticDocuments.Count > 0
            ? FindSemanticRegistrations(productionContext).DistinctBy(registration => registration.Identity).ToArray()
            : FindSyntaxRegistrations(productionContext).ToArray();
        var registeredTypes = registrations
            .SelectMany(registration => registration.TypeKeys)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var duplicateGroup in registrations
            .GroupBy(
                registration => new
                {
                    registration.PrimaryTypeKey,
                    registration.ImplementationTypeKey,
                    registration.Lifetime
                })
            .Where(group => group.Count() > 1))
        {
            var first = duplicateGroup.First();
            issues.Add(CreateIssue(
                "DI001",
                issues.Count,
                "Duplicate dependency injection registration",
                $"{first.TypeName} is registered {duplicateGroup.Count()} times in startup or composition code.",
                IssueSeverity.Warning,
                first.Project,
                first.FilePath,
                first.LineNumber,
                "Consolidate duplicate registrations so service lifetime and implementation choice are unambiguous."));
        }

        var injectedDependencies = context.SemanticDocuments.Count > 0
            ? FindSemanticInjectedDependencies(productionContext)
            : FindSyntaxInjectedDependencies(productionContext);

        foreach (var injectedDependency in injectedDependencies)
        {
            var ownerIsContainerActivated = injectedDependency.OwnerIsFrameworkActivated
                || injectedDependency.OwnerTypeKeys.Any(registeredTypes.Contains);

            if (injectedDependency.TypeKeys.Any(registeredTypes.Contains)
                || !ownerIsContainerActivated
                || IsKnownFrameworkDependency(injectedDependency)
                || !IsLikelyApplicationService(injectedDependency.TypeName))
            {
                continue;
            }

            issues.Add(CreateIssue(
                "DI002",
                issues.Count,
                "Constructor dependency appears unregistered",
                $"{injectedDependency.OwnerType} injects {injectedDependency.TypeName}, but DotDet did not find a matching service registration.",
                IssueSeverity.Warning,
                injectedDependency.Project,
                injectedDependency.FilePath,
                injectedDependency.LineNumber,
                "Register the dependency in Program.cs/Startup.cs or in a source-defined IServiceCollection extension method."));
        }

        foreach (var lifetimeIssue in FindLifetimeIssues(registrations, injectedDependencies, issues.Count))
        {
            issues.Add(lifetimeIssue);
        }

        return issues;
    }

    private static IEnumerable<ServiceRegistration> FindSemanticRegistrations(SolutionAnalysisContext context)
    {
        var documentByPath = context.SemanticDocuments.ToDictionary(
            document => AnalyzerUtilities.NormalizePath(document.FilePath),
            document => document);
        var visitedCompositionMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        foreach (var document in context.SemanticDocuments.Where(IsStartupFile))
        {
            foreach (var invocation in document.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                foreach (var registration in CollectSemanticRegistrations(
                    invocation,
                    document,
                    documentByPath,
                    visitedCompositionMethods,
                    depth: 0))
                {
                    yield return registration;
                }
            }
        }
    }

    private static IEnumerable<ServiceRegistration> CollectSemanticRegistrations(
        InvocationExpressionSyntax invocation,
        SemanticDocumentContext document,
        IReadOnlyDictionary<string, SemanticDocumentContext> documentByPath,
        ISet<IMethodSymbol> visitedCompositionMethods,
        int depth)
    {
        var methodSymbol = GetMethodSymbol(document.SemanticModel, invocation);
        var methodName = methodSymbol?.Name ?? GetInvokedMethodName(invocation);

        if (RegistrationMethods.Contains(methodName)
            && TryExtractSemanticServiceRegistration(document, invocation, methodSymbol, methodName, out var registration))
        {
            yield return registration;
            yield break;
        }

        var declarationSymbol = methodSymbol?.ReducedFrom ?? methodSymbol;

        if (declarationSymbol is null
            || depth >= MaxCompositionDepth
            || !IsSourceCompositionMethod(declarationSymbol)
            || !visitedCompositionMethods.Add(declarationSymbol))
        {
            yield break;
        }

        foreach (var syntaxReference in declarationSymbol.DeclaringSyntaxReferences)
        {
            var declarationPath = AnalyzerUtilities.NormalizePath(syntaxReference.SyntaxTree.FilePath);
            if (syntaxReference.GetSyntax() is not MethodDeclarationSyntax methodDeclaration
                || !documentByPath.TryGetValue(declarationPath, out var declaringDocument))
            {
                continue;
            }

            var cachedMethodDeclaration = declaringDocument.Root.FindNode(methodDeclaration.Span) as MethodDeclarationSyntax
                ?? methodDeclaration;

            foreach (var nestedInvocation in cachedMethodDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                foreach (var nestedRegistration in CollectSemanticRegistrations(
                    nestedInvocation,
                    declaringDocument,
                    documentByPath,
                    visitedCompositionMethods,
                    depth + 1))
                {
                    yield return nestedRegistration;
                }
            }
        }
    }

    private static bool TryExtractSemanticServiceRegistration(
        SemanticDocumentContext document,
        InvocationExpressionSyntax invocation,
        IMethodSymbol? methodSymbol,
        string methodName,
        out ServiceRegistration registration)
    {
        var serviceType = methodSymbol?.TypeArguments.FirstOrDefault();
        var genericTypeArguments = GetGenericTypeArguments(document, invocation);
        serviceType ??= genericTypeArguments.FirstOrDefault();
        if (serviceType is null)
        {
            var firstTypeOf = invocation.ArgumentList.Arguments
                .Select(argument => argument.Expression)
                .OfType<TypeOfExpressionSyntax>()
                .FirstOrDefault();

            serviceType = firstTypeOf is null
                ? null
                : document.SemanticModel.GetTypeInfo(firstTypeOf.Type).Type;
        }

        if (serviceType is null)
        {
            registration = default!;
            return false;
        }

        var typeKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            GetTypeKey(serviceType)
        };

        if (serviceType is INamedTypeSymbol { IsGenericType: true } namedServiceType)
        {
            typeKeys.Add(GetTypeKey(namedServiceType.OriginalDefinition));
        }

        var implementationType = methodSymbol?.TypeArguments.Skip(1).FirstOrDefault();
        implementationType ??= genericTypeArguments.Skip(1).FirstOrDefault();
        if (implementationType is null)
        {
            implementationType = invocation.ArgumentList.Arguments
                .Select(argument => argument.Expression)
                .OfType<TypeOfExpressionSyntax>()
                .Skip(1)
                .Select(typeOfExpression => document.SemanticModel.GetTypeInfo(typeOfExpression.Type).Type)
                .FirstOrDefault(typeSymbol => typeSymbol is not null);
        }

        if (implementationType is not null)
        {
            typeKeys.Add(GetTypeKey(implementationType));

            if (implementationType is INamedTypeSymbol { IsGenericType: true } namedImplementationType)
            {
                typeKeys.Add(GetTypeKey(namedImplementationType.OriginalDefinition));
            }
        }

        registration = new ServiceRegistration(
            TypeName: FormatTypeName(serviceType),
            PrimaryTypeKey: GetTypeKey(serviceType),
            ImplementationTypeKey: implementationType is null ? null : GetTypeKey(implementationType),
            TypeKeys: typeKeys.ToArray(),
            Lifetime: GetLifetime(methodName),
            Project: document.Project,
            FilePath: document.FilePath,
            LineNumber: AnalyzerUtilities.GetLineNumber(invocation));
        return true;
    }

    private static IReadOnlyList<ITypeSymbol> GetGenericTypeArguments(
        SemanticDocumentContext document,
        InvocationExpressionSyntax invocation)
    {
        var genericName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax memberGenericName } => memberGenericName,
            GenericNameSyntax directGenericName => directGenericName,
            _ => null
        };

        if (genericName is null)
        {
            return Array.Empty<ITypeSymbol>();
        }

        return genericName.TypeArgumentList.Arguments
            .Select(typeSyntax => document.SemanticModel.GetTypeInfo(typeSyntax).Type)
            .Where(typeSymbol => typeSymbol is not null)
            .Cast<ITypeSymbol>()
            .ToArray();
    }

    private static IEnumerable<ServiceRegistration> FindSyntaxRegistrations(SolutionAnalysisContext context)
    {
        foreach (var sourceFile in context.SourceFiles.Where(IsStartupFile))
        {
            foreach (var invocation in sourceFile.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                var methodName = memberAccess.Name switch
                {
                    GenericNameSyntax genericName => genericName.Identifier.ValueText,
                    IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
                    _ => memberAccess.Name.ToString()
                };

                if (!RegistrationMethods.Contains(methodName))
                {
                    continue;
                }

                var registration = ExtractSyntaxRegisteredServiceRegistration(memberAccess.Name, invocation);
                if (registration is null)
                {
                    continue;
                }

                yield return new ServiceRegistration(
                    registration.TypeName,
                    registration.PrimaryTypeKey,
                    registration.ImplementationTypeKey,
                    registration.TypeKeys,
                    GetLifetime(methodName),
                    sourceFile.Project,
                    sourceFile.FilePath,
                    AnalyzerUtilities.GetLineNumber(invocation));
            }
        }
    }

    private static SyntaxServiceRegistration? ExtractSyntaxRegisteredServiceRegistration(
        SimpleNameSyntax methodName,
        InvocationExpressionSyntax invocation)
    {
        if (methodName is GenericNameSyntax genericName && genericName.TypeArgumentList.Arguments.Count > 0)
        {
            var typeKeys = genericName.TypeArgumentList.Arguments
                .Select(AnalyzerUtilities.GetTypeIdentifier)
                .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return typeKeys.Length == 0
                ? null
                : new SyntaxServiceRegistration(typeKeys[0], typeKeys[0], typeKeys.Skip(1).FirstOrDefault(), typeKeys);
        }

        var typeOfArguments = invocation.ArgumentList.Arguments
            .Select(argument => argument.Expression)
            .OfType<TypeOfExpressionSyntax>()
            .Select(typeOfExpression => AnalyzerUtilities.GetTypeIdentifier(typeOfExpression.Type))
            .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return typeOfArguments.Length == 0
            ? null
            : new SyntaxServiceRegistration(typeOfArguments[0], typeOfArguments[0], typeOfArguments.Skip(1).FirstOrDefault(), typeOfArguments);
    }

    private static IEnumerable<InjectedDependency> FindSemanticInjectedDependencies(SolutionAnalysisContext context)
    {
        foreach (var document in context.SemanticDocuments)
        {
            foreach (var classDeclaration in document.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var classSymbol = document.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                foreach (var constructor in classDeclaration.Members.OfType<ConstructorDeclarationSyntax>())
                {
                    if (!constructor.Identifier.ValueText.Equals(classDeclaration.Identifier.ValueText, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (var parameter in constructor.ParameterList.Parameters)
                    {
                        if (parameter.Type is null)
                        {
                            continue;
                        }

                        var parameterSymbol = document.SemanticModel.GetDeclaredSymbol(parameter) as IParameterSymbol;
                        var parameterType = parameterSymbol?.Type ?? document.SemanticModel.GetTypeInfo(parameter.Type).Type;
                        if (parameterType is null)
                        {
                            continue;
                        }

                        yield return new InjectedDependency(
                            TypeName: FormatTypeName(parameterType),
                            TypeKeys: GetTypeKeys(parameterType),
                            Namespace: parameterType.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                            OwnerType: classSymbol?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                                ?? classDeclaration.Identifier.ValueText,
                            OwnerTypeKeys: classSymbol is null ? [classDeclaration.Identifier.ValueText] : GetTypeKeys(classSymbol),
                            OwnerIsFrameworkActivated: IsFrameworkActivatedType(classSymbol, classDeclaration),
                            Project: document.Project,
                            FilePath: document.FilePath,
                            LineNumber: AnalyzerUtilities.GetLineNumber(parameter));
                    }
                }
            }
        }
    }

    private static IEnumerable<InjectedDependency> FindSyntaxInjectedDependencies(SolutionAnalysisContext context)
    {
        foreach (var sourceFile in context.SourceFiles)
        {
            foreach (var classDeclaration in sourceFile.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                foreach (var constructor in classDeclaration.Members.OfType<ConstructorDeclarationSyntax>())
                {
                    if (!constructor.Identifier.ValueText.Equals(classDeclaration.Identifier.ValueText, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (var parameter in constructor.ParameterList.Parameters)
                    {
                        if (parameter.Type is null)
                        {
                            continue;
                        }

                        var typeName = AnalyzerUtilities.GetTypeIdentifier(parameter.Type);
                        yield return new InjectedDependency(
                            typeName,
                            new[] { typeName },
                            string.Empty,
                            classDeclaration.Identifier.ValueText,
                            new[] { classDeclaration.Identifier.ValueText },
                            IsFrameworkActivatedType(classDeclaration),
                            sourceFile.Project,
                            sourceFile.FilePath,
                            AnalyzerUtilities.GetLineNumber(parameter));
                    }
                }
            }
        }
    }

    private static IMethodSymbol? GetMethodSymbol(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        var symbol = semanticModel.GetSymbolInfo(invocation).Symbol
            ?? semanticModel.GetSymbolInfo(invocation.Expression).Symbol;

        return symbol switch
        {
            IMethodSymbol methodSymbol => methodSymbol,
            _ => null
        };
    }

    private static string GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name switch
            {
                GenericNameSyntax genericName => genericName.Identifier.ValueText,
                IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
                _ => memberAccess.Name.ToString()
            },
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            _ => string.Empty
        };
    }

    private static bool IsSourceCompositionMethod(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.DeclaringSyntaxReferences.Length == 0)
        {
            return false;
        }

        return methodSymbol.Name.StartsWith("Add", StringComparison.OrdinalIgnoreCase)
            || methodSymbol.Name.StartsWith("Configure", StringComparison.OrdinalIgnoreCase)
            || methodSymbol.Parameters.Any(parameter => IsCompositionType(parameter.Type))
            || IsCompositionType(methodSymbol.ReturnType);
    }

    private static bool IsCompositionType(ITypeSymbol typeSymbol)
    {
        var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return typeName.Contains("IServiceCollection", StringComparison.Ordinal)
            || typeName.Contains("WebApplicationBuilder", StringComparison.Ordinal)
            || typeName.Contains("IHostApplicationBuilder", StringComparison.Ordinal);
    }

    private static bool IsStartupFile(SourceFileContext sourceFile)
    {
        var fileName = Path.GetFileName(sourceFile.FilePath);
        return fileName.Equals("Program.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Startup.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStartupFile(SemanticDocumentContext document)
    {
        var fileName = Path.GetFileName(document.FilePath);
        return fileName.Equals("Program.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Startup.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownFrameworkDependency(InjectedDependency dependency)
    {
        if (AnalyzerUtilities.IsLikelyFrameworkService(dependency.TypeName))
        {
            return true;
        }

        return dependency.Namespace.StartsWith("System", StringComparison.Ordinal)
            || dependency.Namespace.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)
            || dependency.Namespace.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || dependency.Namespace.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || dependency.Namespace.Equals("AutoMapper", StringComparison.Ordinal);
    }

    private static bool IsFrameworkActivatedType(INamedTypeSymbol? classSymbol, ClassDeclarationSyntax classDeclaration)
    {
        if (classDeclaration.Identifier.ValueText.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (classSymbol is null)
        {
            return IsFrameworkActivatedType(classDeclaration);
        }

        return InheritsFrom(classSymbol, "Microsoft.AspNetCore.Mvc.ControllerBase", "Microsoft.AspNetCore.Mvc.Controller", "Microsoft.Extensions.Hosting.BackgroundService")
            || classSymbol.AllInterfaces.Any(@interface =>
                @interface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", string.Empty, StringComparison.Ordinal)
                    .Equals("Microsoft.Extensions.Hosting.IHostedService", StringComparison.Ordinal));
    }

    private static bool IsFrameworkActivatedType(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Identifier.ValueText.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            || classDeclaration.BaseList?.Types.Any(type =>
                type.Type.ToString().Contains("ControllerBase", StringComparison.Ordinal)
                || type.Type.ToString().Contains("Controller", StringComparison.Ordinal)
                || type.Type.ToString().Contains("BackgroundService", StringComparison.Ordinal)
                || type.Type.ToString().Contains("IHostedService", StringComparison.Ordinal)) == true;
    }

    private static bool InheritsFrom(ITypeSymbol? typeSymbol, params string[] metadataNames)
    {
        for (var current = typeSymbol; current is not null; current = current.BaseType)
        {
            var fullyQualifiedName = current
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty, StringComparison.Ordinal);

            if (metadataNames.Any(metadataName =>
                    fullyQualifiedName.Equals(metadataName, StringComparison.Ordinal)
                    || current.Name.Equals(metadataName.Split('.').Last(), StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLikelyApplicationService(string typeName)
    {
        return typeName.StartsWith('I') && typeName.Length > 1 && char.IsUpper(typeName[1])
            || typeName.EndsWith("Service", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Repository", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Handler", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Client", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Provider", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Factory", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Validator", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<AnalysisIssue> FindLifetimeIssues(
        IReadOnlyList<ServiceRegistration> registrations,
        IEnumerable<InjectedDependency> injectedDependencies,
        int startIndex)
    {
        var registrationsByTypeKey = registrations
            .SelectMany(registration => registration.TypeKeys.Select(typeKey => (TypeKey: typeKey, Registration: registration)))
            .GroupBy(pair => pair.TypeKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Registration).ToArray(), StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        var index = startIndex;

        foreach (var injectedDependency in injectedDependencies)
        {
            var ownerRegistration = injectedDependency.OwnerTypeKeys
                .SelectMany(typeKey => registrationsByTypeKey.GetValueOrDefault(typeKey) ?? Array.Empty<ServiceRegistration>())
                .FirstOrDefault(registration => registration.Lifetime == ServiceLifetimeKind.Singleton);

            if (ownerRegistration is null)
            {
                continue;
            }

            var dependencyRegistration = injectedDependency.TypeKeys
                .SelectMany(typeKey => registrationsByTypeKey.GetValueOrDefault(typeKey) ?? Array.Empty<ServiceRegistration>())
                .FirstOrDefault(registration => registration.Lifetime == ServiceLifetimeKind.Scoped);

            if (dependencyRegistration is null)
            {
                continue;
            }

            var reportKey = $"{ownerRegistration.PrimaryTypeKey}|{dependencyRegistration.PrimaryTypeKey}";
            if (!reported.Add(reportKey))
            {
                continue;
            }

            yield return CreateIssue(
                "DI003",
                index++,
                "Singleton service captures scoped dependency",
                $"{injectedDependency.OwnerType} is registered as singleton but injects scoped dependency {injectedDependency.TypeName}.",
                IssueSeverity.Error,
                injectedDependency.Project,
                injectedDependency.FilePath,
                injectedDependency.LineNumber,
                "Change the consumer to a scoped lifetime or resolve scoped dependencies through IServiceScopeFactory inside a controlled scope.");
        }
    }

    private static ServiceLifetimeKind GetLifetime(string methodName)
    {
        if (SingletonRegistrationMethods.Contains(methodName))
        {
            return ServiceLifetimeKind.Singleton;
        }

        if (ScopedRegistrationMethods.Contains(methodName))
        {
            return ServiceLifetimeKind.Scoped;
        }

        return ServiceLifetimeKind.Transient;
    }

    private static string FormatTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    private static string GetTypeKey(ITypeSymbol typeSymbol)
    {
        return typeSymbol
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> GetTypeKeys(ITypeSymbol typeSymbol)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            GetTypeKey(typeSymbol)
        };

        if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol)
        {
            keys.Add(GetTypeKey(namedTypeSymbol.OriginalDefinition));
        }

        return keys.ToArray();
    }

    private static AnalysisIssue CreateIssue(
        string ruleId,
        int index,
        string title,
        string description,
        IssueSeverity severity,
        AnalyzedProject project,
        string filePath,
        int lineNumber,
        string recommendation)
    {
        return new AnalysisIssue
        {
            Id = $"{ruleId}-{index + 1:D3}",
            RuleId = ruleId,
            Title = title,
            Description = description,
            Severity = severity,
            Category = AnalysisCategories.DependencyInjection,
            ProjectName = project.Name,
            FilePath = filePath,
            LineNumber = lineNumber,
            Recommendation = recommendation,
            DetectionMethod = lineNumber > 0
                ? IssueEnrichmentService.RoslynSemanticAnalysis
                : IssueEnrichmentService.RoslynSyntaxAnalysis,
            Confidence = ruleId switch
            {
                "DI002" => IssueConfidence.Medium,
                _ => IssueConfidence.High
            },
            WhyDetected = AnalyzerUtilities.BuildEvidence(
                ("Rule", ruleId),
                ("Project", project.Name),
                ("File", filePath),
                ("Line", lineNumber.ToString()),
                ("Detected", description),
                ("Applicability", ruleId == "DI002"
                    ? "Constructor owner appears to be activated by dependency injection."
                    : "Production dependency-injection composition and constructor graph."))
        };
    }

    private sealed record ServiceRegistration(
        string TypeName,
        string PrimaryTypeKey,
        string? ImplementationTypeKey,
        IReadOnlyList<string> TypeKeys,
        ServiceLifetimeKind Lifetime,
        AnalyzedProject Project,
        string FilePath,
        int LineNumber)
    {
        public string Identity => $"{PrimaryTypeKey}|{FilePath}|{LineNumber}";
    }

    private sealed record InjectedDependency(
        string TypeName,
        IReadOnlyList<string> TypeKeys,
        string Namespace,
        string OwnerType,
        IReadOnlyList<string> OwnerTypeKeys,
        bool OwnerIsFrameworkActivated,
        AnalyzedProject Project,
        string FilePath,
        int LineNumber);

    private sealed record SyntaxServiceRegistration(
        string TypeName,
        string PrimaryTypeKey,
        string? ImplementationTypeKey,
        IReadOnlyList<string> TypeKeys);

    private enum ServiceLifetimeKind
    {
        Transient,
        Scoped,
        Singleton
    }
}
