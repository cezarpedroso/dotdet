using Forge.Api.Analysis;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Forge.Api.Analyzers;

public sealed class EfCoreAnalyzer
{
    private static readonly HashSet<string> DestructiveMigrationOperations = new(StringComparer.Ordinal)
    {
        "DropTable",
        "DropColumn",
        "DropForeignKey",
        "DropIndex",
        "DropPrimaryKey",
        "DropUniqueConstraint",
        "DropCheckConstraint"
    };

    private readonly SemanticAnalysisHelper _semanticHelper;

    public EfCoreAnalyzer(SemanticAnalysisHelper semanticHelper)
    {
        _semanticHelper = semanticHelper;
    }

    public IReadOnlyList<AnalysisIssue> Analyze(SolutionAnalysisContext context)
    {
        var issues = new List<AnalysisIssue>();
        var productionContext = context with
        {
            Projects = context.Projects.Where(AnalyzerUtilities.IsProductionProject).ToArray(),
            SourceFiles = context.SourceFiles.Where(file => AnalyzerUtilities.IsProductionProject(file.Project)).ToArray(),
            SemanticDocuments = context.SemanticDocuments.Where(document => AnalyzerUtilities.IsProductionProject(document.Project)).ToArray(),
            SemanticProjects = context.SemanticProjects.Where(project => AnalyzerUtilities.IsProductionProject(project.Project)).ToArray()
        };
        var dbContexts = FindDbContexts(productionContext).ToArray();
        var configuredEntityModel = FindConfiguredEntityModel(productionContext);
        var entityClasses = (productionContext.SemanticDocuments.Count > 0
                ? productionContext.SemanticDocuments.SelectMany(file => file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Select(classDeclaration => new EntityClass(
                        file.Project,
                        file.FilePath,
                        file.SemanticModel,
                        classDeclaration,
                        file.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol)))
                : productionContext.SourceFiles.SelectMany(file => file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Select(classDeclaration => new EntityClass(file.Project, file.FilePath, null, classDeclaration, null))))
            .ToArray();
        var entityClassesByName = entityClasses
            .GroupBy(entity => entity.ClassDeclaration.Identifier.ValueText, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var entityClassesByTypeKey = entityClasses
            .Where(entity => !string.IsNullOrWhiteSpace(entity.TypeKey))
            .GroupBy(entity => entity.TypeKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var project in productionContext.Projects.Where(project => AnalyzerUtilities.ProjectHasPackage(project, "Microsoft.EntityFrameworkCore")))
        {
            if (dbContexts.All(dbContext => dbContext.Project != project))
            {
                issues.Add(CreateIssue(
                    "EF001",
                    issues.Count,
                    "EF Core package referenced without a DbContext",
                    $"{project.Name} references EF Core, but DotDet did not find a DbContext class in that project.",
                    IssueSeverity.Info,
                    project,
                    project.FilePath,
                    null,
                    "Remove unused EF Core packages or add the intended DbContext so data access is explicit."));
            }
        }

        foreach (var dbContext in dbContexts)
        {
            if (dbContext.DbSetEntities.Count == 0)
            {
                issues.Add(CreateIssue(
                    "EF002",
                    issues.Count,
                    "DbContext has no DbSet properties",
                    $"{dbContext.ClassName} does not expose any DbSet<TEntity> properties.",
                    IssueSeverity.Info,
                    dbContext.Project,
                    dbContext.FilePath,
                    dbContext.LineNumber,
                    "Add DbSet properties for aggregate roots or confirm the context is intentionally model-built only.",
                    dbContext.DetectionMethod));
            }

            foreach (var entity in dbContext.DbSetEntities)
            {
                var entityClass = entity.TypeKey is not null && entityClassesByTypeKey.TryGetValue(entity.TypeKey, out var semanticEntityClass)
                    ? semanticEntityClass
                    : entityClassesByName.GetValueOrDefault(entity.Name);

                if (entityClass is null
                    || HasObviousPrimaryKey(entityClass)
                    || IsKeylessEntity(entityClass)
                    || (entityClass.TypeKey is not null
                        && (configuredEntityModel.KeyedEntityTypeKeys.Contains(entityClass.TypeKey)
                            || configuredEntityModel.KeylessEntityTypeKeys.Contains(entityClass.TypeKey)))
                    || configuredEntityModel.KeyedEntityNames.Contains(entity.Name)
                    || configuredEntityModel.KeylessEntityNames.Contains(entity.Name))
                {
                    continue;
                }

                issues.Add(CreateIssue(
                    "EF003",
                    issues.Count,
                    "Entity is missing an obvious primary key",
                    $"{entity.Name} is exposed from {dbContext.ClassName}, but DotDet did not find Id, {entity.Name}Id, [Key], inherited keys, or HasKey configuration.",
                    IssueSeverity.Warning,
                    entityClass.Project,
                    entityClass.FilePath,
                    AnalyzerUtilities.GetLineNumber(entityClass.ClassDeclaration),
                    "Add a conventional primary key property or configure the key explicitly in OnModelCreating.",
                    entityClass.SemanticModel is null
                        ? IssueEnrichmentService.RoslynSyntaxAnalysis
                        : IssueEnrichmentService.RoslynSemanticAnalysis));
            }
        }

        foreach (var migrationOperation in FindRiskyMigrationOperations(productionContext))
        {
            var isDestructiveSchemaChange = DestructiveMigrationOperations.Contains(migrationOperation.Operation);
            issues.Add(CreateIssue(
                isDestructiveSchemaChange ? "EF004" : "EF005",
                issues.Count,
                isDestructiveSchemaChange ? "Migration contains destructive schema operation" : "Migration executes raw SQL",
                $"{migrationOperation.FileName} calls migrationBuilder.{migrationOperation.Operation}.",
                IssueSeverity.Warning,
                migrationOperation.Project,
                migrationOperation.FilePath,
                migrationOperation.LineNumber,
                isDestructiveSchemaChange
                    ? "Review data-loss impact, add backups or expand/contract migration steps, and document the deployment plan."
                    : "Prefer provider-safe migration APIs when possible, and document why raw SQL is required.",
                migrationOperation.DetectionMethod));
        }

        foreach (var dbContextGroup in dbContexts.GroupBy(dbContext => dbContext.Project))
        {
            var projectMigrationCount = productionContext.SourceFiles.Count(file =>
                file.Project == dbContextGroup.Key && IsMigrationFile(file));

            if (projectMigrationCount == 0)
            {
                issues.Add(CreateIssue(
                    "EF006",
                    issues.Count,
                    "DbContext project has no migrations",
                    $"{dbContextGroup.Key.Name} defines DbContext classes, but DotDet did not find migration files.",
                    IssueSeverity.Info,
                    dbContextGroup.Key,
                    dbContextGroup.Key.FilePath,
                    null,
                    "Add migrations or document the external migration strategy used for this data store.",
                    IssueEnrichmentService.MsBuildProjectConfiguration));
            }
        }

        return issues;
    }

    private IEnumerable<DbContextInfo> FindDbContexts(SolutionAnalysisContext context)
    {
        return context.SemanticDocuments.Count > 0
            ? FindSemanticDbContexts(context)
            : FindSyntaxDbContexts(context);
    }

    private IEnumerable<DbContextInfo> FindSemanticDbContexts(SolutionAnalysisContext context)
    {
        foreach (var document in context.SemanticDocuments)
        {
            foreach (var classDeclaration in document.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var classSymbol = document.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                if (!InheritsFrom(classSymbol, "Microsoft.EntityFrameworkCore.DbContext", "DbContext"))
                {
                    continue;
                }

                var dbSetEntities = classDeclaration.Members
                    .OfType<PropertyDeclarationSyntax>()
                    .Select(property => document.SemanticModel.GetDeclaredSymbol(property) as IPropertySymbol)
                    .Where(property => property?.Type is INamedTypeSymbol namedType
                        && namedType.Name.Equals("DbSet", StringComparison.Ordinal)
                        && namedType.TypeArguments.Length == 1)
                    .Select(property =>
                    {
                        var entityType = ((INamedTypeSymbol)property!.Type).TypeArguments[0];
                        return new DbSetEntityInfo(entityType.Name, _semanticHelper.GetTypeKey(entityType));
                    })
                    .DistinctBy(entity => entity.TypeKey ?? entity.Name, StringComparer.Ordinal)
                    .ToArray();

                yield return new DbContextInfo(
                    classSymbol?.Name ?? classDeclaration.Identifier.ValueText,
                    document.Project,
                    document.FilePath,
                    AnalyzerUtilities.GetLineNumber(classDeclaration),
                    dbSetEntities,
                    IssueEnrichmentService.RoslynSemanticAnalysis);
            }
        }
    }

    private static IEnumerable<DbContextInfo> FindSyntaxDbContexts(SolutionAnalysisContext context)
    {
        foreach (var sourceFile in context.SourceFiles)
        {
            foreach (var classDeclaration in sourceFile.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!InheritsFrom(classDeclaration, "DbContext"))
                {
                    continue;
                }

                var dbSetEntities = classDeclaration.Members
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(property => IsDbSetType(property.Type))
                    .Select(property => ExtractDbSetEntityName(property.Type))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(name => new DbSetEntityInfo(name, null))
                    .ToArray();

                yield return new DbContextInfo(
                    classDeclaration.Identifier.ValueText,
                    sourceFile.Project,
                    sourceFile.FilePath,
                    AnalyzerUtilities.GetLineNumber(classDeclaration),
                    dbSetEntities,
                    IssueEnrichmentService.RoslynSyntaxAnalysis);
            }
        }
    }

    private IEnumerable<MigrationOperation> FindRiskyMigrationOperations(SolutionAnalysisContext context)
    {
        return context.SemanticDocuments.Count > 0
            ? FindSemanticRiskyMigrationOperations(context)
            : FindSyntaxRiskyMigrationOperations(context);
    }

    private IEnumerable<MigrationOperation> FindSemanticRiskyMigrationOperations(SolutionAnalysisContext context)
    {
        foreach (var document in context.SemanticDocuments.Where(IsMigrationFile))
        {
            foreach (var invocation in document.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsInsideMigrationUpMethod(invocation))
                {
                    continue;
                }

                var methodSymbol = document.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol
                    ?? document.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
                var operationName = methodSymbol?.Name ?? GetInvokedMethodName(invocation);

                if (operationName is null || !IsRiskyMigrationOperation(operationName))
                {
                    continue;
                }

                yield return new MigrationOperation(
                    operationName,
                    document.Project,
                    document.FilePath,
                    Path.GetFileName(document.FilePath),
                    AnalyzerUtilities.GetLineNumber(invocation),
                    IssueEnrichmentService.RoslynSemanticAnalysis);
            }
        }
    }

    private static IEnumerable<MigrationOperation> FindSyntaxRiskyMigrationOperations(SolutionAnalysisContext context)
    {
        foreach (var sourceFile in context.SourceFiles.Where(IsMigrationFile))
        {
            foreach (var invocation in sourceFile.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsInsideMigrationUpMethod(invocation))
                {
                    continue;
                }

                var operationName = invocation.Expression switch
                {
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                    IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
                    _ => null
                };

                if (operationName is null || !IsRiskyMigrationOperation(operationName))
                {
                    continue;
                }

                yield return new MigrationOperation(
                    operationName,
                    sourceFile.Project,
                    sourceFile.FilePath,
                    Path.GetFileName(sourceFile.FilePath),
                    AnalyzerUtilities.GetLineNumber(invocation),
                    IssueEnrichmentService.RoslynSyntaxAnalysis);
            }
        }
    }

    private static bool IsInsideMigrationUpMethod(SyntaxNode node)
    {
        return node.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault()?
            .Identifier
            .ValueText
            .Equals("Up", StringComparison.Ordinal) == true;
    }

    private static bool IsMigrationFile(SourceFileContext sourceFile)
    {
        return sourceFile.FilePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("Migrations", StringComparison.OrdinalIgnoreCase))
            || sourceFile.Root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Any(classDeclaration => InheritsFrom(classDeclaration, "Migration"));
    }

    private bool IsMigrationFile(SemanticDocumentContext document)
    {
        return document.FilePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("Migrations", StringComparison.OrdinalIgnoreCase))
            || document.Root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Any(classDeclaration => InheritsFrom(
                    document.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol,
                    "Microsoft.EntityFrameworkCore.Migrations.Migration",
                    "Migration"));
    }

    private static bool InheritsFrom(ClassDeclarationSyntax classDeclaration, string baseTypeName)
    {
        return classDeclaration.BaseList?.Types.Any(type =>
            type.Type.ToString().Split('.', '<').Last().Equals(baseTypeName, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private bool InheritsFrom(ITypeSymbol? typeSymbol, string metadataName, string fallbackName)
    {
        return _semanticHelper.InheritsFrom(typeSymbol, metadataName, fallbackName);
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

    private static bool IsRiskyMigrationOperation(string? operationName)
    {
        return operationName is "Sql"
            || (operationName is not null && DestructiveMigrationOperations.Contains(operationName));
    }

    private static bool IsDbSetType(TypeSyntax type)
    {
        return type switch
        {
            GenericNameSyntax genericName => genericName.Identifier.ValueText.Equals("DbSet", StringComparison.OrdinalIgnoreCase),
            QualifiedNameSyntax qualifiedName => IsDbSetType(qualifiedName.Right),
            _ => false
        };
    }

    private static string? ExtractDbSetEntityName(TypeSyntax type)
    {
        return type switch
        {
            GenericNameSyntax genericName when genericName.TypeArgumentList.Arguments.Count == 1 =>
                AnalyzerUtilities.GetTypeIdentifier(genericName.TypeArgumentList.Arguments[0]),
            QualifiedNameSyntax qualifiedName => ExtractDbSetEntityName(qualifiedName.Right),
            _ => null
        };
    }

    private static bool HasObviousPrimaryKey(EntityClass entityClass)
    {
        var className = entityClass.ClassDeclaration.Identifier.ValueText;

        if (entityClass.ClassSymbol is not null)
        {
            for (var current = entityClass.ClassSymbol; current is not null; current = current.BaseType)
            {
                if (current.GetMembers().OfType<IPropertySymbol>().Any(property =>
                        IsConventionalKeyName(property.Name, className)
                        || HasKeyAttribute(property)))
                {
                    return true;
                }
            }
        }

        return entityClass.ClassDeclaration.Members.OfType<PropertyDeclarationSyntax>().Any(property =>
            IsConventionalKeyName(property.Identifier.ValueText, className)
            || HasKeyAttribute(entityClass, property));
    }

    private static bool IsConventionalKeyName(string propertyName, string className)
    {
        return propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals($"{className}Id", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKeylessEntity(EntityClass entityClass)
    {
        if (entityClass.ClassSymbol is not null)
        {
            return entityClass.ClassSymbol.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name is "Keyless" or "KeylessAttribute"
                || attribute.AttributeClass?.ToDisplayString() == "Microsoft.EntityFrameworkCore.KeylessAttribute");
        }

        return entityClass.ClassDeclaration.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
            attribute.Name.ToString().Equals("Keyless", StringComparison.OrdinalIgnoreCase)
            || attribute.Name.ToString().Equals("KeylessAttribute", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasKeyAttribute(IPropertySymbol propertySymbol)
    {
        return propertySymbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name is "Key" or "KeyAttribute"
            || attribute.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.KeyAttribute");
    }

    private static bool HasKeyAttribute(EntityClass entityClass, PropertyDeclarationSyntax property)
    {
        if (entityClass.SemanticModel is not null
            && entityClass.SemanticModel.GetDeclaredSymbol(property) is IPropertySymbol propertySymbol)
        {
            return propertySymbol.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name is "Key" or "KeyAttribute"
                || attribute.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.KeyAttribute");
        }

        return property.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
            attribute.Name.ToString().Equals("Key", StringComparison.OrdinalIgnoreCase)
            || attribute.Name.ToString().Equals("KeyAttribute", StringComparison.OrdinalIgnoreCase));
    }

    private EntityModelConfiguration FindConfiguredEntityModel(SolutionAnalysisContext context)
    {
        var keyedEntityTypeKeys = new HashSet<string>(StringComparer.Ordinal);
        var keylessEntityTypeKeys = new HashSet<string>(StringComparer.Ordinal);
        var keyedEntityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keylessEntityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in context.SemanticDocuments)
        {
            foreach (var invocation in document.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var methodSymbol = _semanticHelper.GetMethodSymbol(document.SemanticModel, invocation);
                var methodName = methodSymbol?.Name ?? methodSymbol?.ReducedFrom?.Name ?? GetInvokedMethodName(invocation);
                if (methodName is not ("HasKey" or "HasNoKey"))
                {
                    continue;
                }

                var entityType = GetConfiguredEntityType(methodSymbol);
                var entityName = entityType?.Name ?? GetConfiguredEntityName(invocation);
                if (entityType is null && string.IsNullOrWhiteSpace(entityName))
                {
                    continue;
                }

                if (methodName == "HasNoKey")
                {
                    if (entityType is not null)
                    {
                        keylessEntityTypeKeys.Add(_semanticHelper.GetTypeKey(entityType));
                    }

                    if (!string.IsNullOrWhiteSpace(entityName))
                    {
                        keylessEntityNames.Add(entityName);
                    }
                }
                else
                {
                    if (entityType is not null)
                    {
                        keyedEntityTypeKeys.Add(_semanticHelper.GetTypeKey(entityType));
                    }

                    if (!string.IsNullOrWhiteSpace(entityName))
                    {
                        keyedEntityNames.Add(entityName);
                    }
                }
            }
        }

        return new EntityModelConfiguration(keyedEntityTypeKeys, keylessEntityTypeKeys, keyedEntityNames, keylessEntityNames);
    }

    private static ITypeSymbol? GetConfiguredEntityType(IMethodSymbol? methodSymbol)
    {
        if (methodSymbol?.ContainingType is INamedTypeSymbol { TypeArguments.Length: 1 } containingType
            && containingType.Name.Equals("EntityTypeBuilder", StringComparison.Ordinal))
        {
            return containingType.TypeArguments[0];
        }

        if (methodSymbol?.ReducedFrom?.ContainingType is INamedTypeSymbol { TypeArguments.Length: 1 } reducedContainingType
            && reducedContainingType.Name.Equals("EntityTypeBuilder", StringComparison.Ordinal))
        {
            return reducedContainingType.TypeArguments[0];
        }

        return null;
    }

    private static string? GetConfiguredEntityName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        return memberAccess.Expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Select(nestedInvocation => nestedInvocation.Expression)
            .OfType<MemberAccessExpressionSyntax>()
            .Select(member => member.Name)
            .OfType<GenericNameSyntax>()
            .Where(genericName => genericName.Identifier.ValueText.Equals("Entity", StringComparison.Ordinal))
            .Select(genericName => genericName.TypeArgumentList.Arguments.FirstOrDefault())
            .Where(type => type is not null)
            .Select(type => AnalyzerUtilities.GetTypeIdentifier(type!))
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
    }

    private static AnalysisIssue CreateIssue(
        string ruleId,
        int index,
        string title,
        string description,
        IssueSeverity severity,
        AnalyzedProject project,
        string filePath,
        int? lineNumber,
        string recommendation,
        string? detectionMethod = null)
    {
        return new AnalysisIssue
        {
            Id = $"{ruleId}-{index + 1:D3}",
            RuleId = ruleId,
            Title = title,
            Description = description,
            Severity = severity,
            Category = AnalysisCategories.EfCore,
            ProjectName = project.Name,
            FilePath = filePath,
            LineNumber = lineNumber,
            Recommendation = recommendation,
            DetectionMethod = detectionMethod,
            WhyDetected = AnalyzerUtilities.BuildEvidence(
                ("Rule", ruleId),
                ("Project", project.Name),
                ("File", filePath),
                ("Line", lineNumber?.ToString()),
                ("Detected", description),
                ("Applicability", "Production project with EF Core package references, DbContext types, or migration files."))
        };
    }

    private sealed record DbSetEntityInfo(string Name, string? TypeKey);

    private sealed record DbContextInfo(
        string ClassName,
        AnalyzedProject Project,
        string FilePath,
        int LineNumber,
        IReadOnlyList<DbSetEntityInfo> DbSetEntities,
        string DetectionMethod);

    private sealed record EntityClass(
        AnalyzedProject Project,
        string FilePath,
        SemanticModel? SemanticModel,
        ClassDeclarationSyntax ClassDeclaration,
        INamedTypeSymbol? ClassSymbol)
    {
        public string? TypeKey => ClassSymbol is null
            ? null
            : ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty, StringComparison.Ordinal);
    }

    private sealed record MigrationOperation(
        string Operation,
        AnalyzedProject Project,
        string FilePath,
        string FileName,
        int LineNumber,
        string DetectionMethod);

    private sealed record EntityModelConfiguration(
        IReadOnlySet<string> KeyedEntityTypeKeys,
        IReadOnlySet<string> KeylessEntityTypeKeys,
        IReadOnlySet<string> KeyedEntityNames,
        IReadOnlySet<string> KeylessEntityNames);
}
