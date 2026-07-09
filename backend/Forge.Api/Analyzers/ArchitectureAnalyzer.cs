using Forge.Api.Analysis;
using Forge.Api.Models;
using Forge.Api.Services;

namespace Forge.Api.Analyzers;

public sealed class ArchitectureAnalyzer
{
    private readonly SemanticAnalysisHelper _semanticHelper;

    public ArchitectureAnalyzer(SemanticAnalysisHelper semanticHelper)
    {
        _semanticHelper = semanticHelper;
    }

    public IReadOnlyList<AnalysisIssue> Analyze(SolutionAnalysisContext context)
    {
        var issues = new List<AnalysisIssue>();
        var productionProjects = context.Projects
            .Where(AnalyzerUtilities.IsProductionProject)
            .ToArray();

        if (productionProjects.Length < 2)
        {
            return issues;
        }

        foreach (var project in productionProjects)
        {
            var referencedProjects = _semanticHelper.GetReferencedProjects(context, project)
                .Where(AnalyzerUtilities.IsProductionProject)
                .ToArray();

            if (IsDomainProject(project))
            {
                foreach (var referencedProject in referencedProjects.Where(IsInfrastructureOrApiProject))
                {
                    issues.Add(CreateProjectDependencyIssue(
                        context,
                        ruleId: "ARCH001",
                        index: issues.Count,
                        title: "Domain layer depends on an outer layer",
                        description: $"{project.Name} references {referencedProject.Name}. Domain projects should remain independent of infrastructure and delivery concerns.",
                        severity: IssueSeverity.Error,
                        project,
                        referencedProject,
                        "Move abstractions into the Domain or Application layer and invert the dependency so Infrastructure/API implements them."));
                }

                if (ReferencesEntityFramework(context, project))
                {
                    issues.Add(CreateFrameworkDependencyIssue(
                        context,
                        ruleId: "ARCH002",
                        index: issues.Count,
                        title: "Domain project references framework infrastructure",
                        description: $"{project.Name} depends on EF Core types or assemblies, which couples the domain model to persistence concerns.",
                        severity: IssueSeverity.Error,
                        project,
                        frameworkLabel: "EF Core",
                        typePredicate: typeSymbol =>
                            typeSymbol.ContainingAssembly?.Name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) == true
                            || typeSymbol.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal),
                        recommendation: "Keep domain projects framework-agnostic; move persistence concerns into Infrastructure or Application boundaries."));
                }

                if (ReferencesAspNetCore(context, project))
                {
                    issues.Add(CreateFrameworkDependencyIssue(
                        context,
                        ruleId: "ARCH002",
                        index: issues.Count,
                        title: "Domain project references framework infrastructure",
                        description: $"{project.Name} depends on ASP.NET Core types or assemblies, which couples the domain model to delivery concerns.",
                        severity: IssueSeverity.Error,
                        project,
                        frameworkLabel: "ASP.NET Core",
                        typePredicate: typeSymbol =>
                            typeSymbol.ContainingAssembly?.Name.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase) == true
                            || typeSymbol.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal),
                        recommendation: "Keep domain projects framework-agnostic; move ASP.NET concerns into API or Web projects."));
                }
            }

            if (IsApplicationProject(project))
            {
                foreach (var referencedProject in referencedProjects.Where(IsInfrastructureProject))
                {
                    issues.Add(CreateProjectDependencyIssue(
                        context,
                        ruleId: "ARCH003",
                        index: issues.Count,
                        title: "Application layer references Infrastructure directly",
                        description: $"{project.Name} references {referencedProject.Name}. Application code should depend on abstractions, not concrete infrastructure.",
                        severity: IssueSeverity.Warning,
                        project,
                        referencedProject,
                        "Introduce interfaces in Application and implement them from Infrastructure with dependency injection at the composition root."));
                }
            }

            if (IsLowerLayer(project))
            {
                foreach (var referencedProject in referencedProjects.Where(IsApiOrWebProject))
                {
                    issues.Add(CreateProjectDependencyIssue(
                        context,
                        ruleId: "ARCH004",
                        index: issues.Count,
                        title: "Lower layer references API/Web project",
                        description: $"{project.Name} references {referencedProject.Name}. API/Web projects should sit at the outer edge of the dependency graph.",
                        severity: IssueSeverity.Error,
                        project,
                        referencedProject,
                        "Reverse this dependency by moving shared contracts downward or introducing an application boundary."));
                }
            }
        }

        foreach (var cycle in FindCycles(context))
        {
            issues.Add(CreateIssue(
                "ARCH005",
                issues.Count,
                "Circular project dependency detected",
                $"The project graph contains a cycle: {string.Join(" -> ", cycle)}.",
                IssueSeverity.Critical,
                context.Projects.FirstOrDefault(project => project.Name.Equals(cycle[0], StringComparison.OrdinalIgnoreCase)),
                null,
                null,
                "Break the cycle by extracting shared abstractions into a lower-level project or removing one of the project references."));
        }

        return issues;
    }

    private AnalysisIssue CreateProjectDependencyIssue(
        SolutionAnalysisContext context,
        string ruleId,
        int index,
        string title,
        string description,
        IssueSeverity severity,
        AnalyzedProject project,
        AnalyzedProject referencedProject,
        string recommendation)
    {
        var reference = _semanticHelper.FindFirstTypeReference(
            context,
            project,
            typeSymbol => _semanticHelper.TypeBelongsToProject(context, typeSymbol, referencedProject));

        var resolvedDescription = reference is null
            ? description
            : $"{description} DotDet traced the dependency through {reference.TypeDisplayName}.";

        return CreateIssue(
            ruleId,
            index,
            title,
            resolvedDescription,
            severity,
            project,
            reference?.FilePath ?? project.FilePath,
            reference?.LineNumber,
            recommendation);
    }

    private AnalysisIssue CreateFrameworkDependencyIssue(
        SolutionAnalysisContext context,
        string ruleId,
        int index,
        string title,
        string description,
        IssueSeverity severity,
        AnalyzedProject project,
        string frameworkLabel,
        Func<Microsoft.CodeAnalysis.ITypeSymbol, bool> typePredicate,
        string recommendation)
    {
        var reference = _semanticHelper.FindFirstTypeReference(context, project, typePredicate);
        var resolvedDescription = reference is null
            ? description
            : $"{description} DotDet found a direct {frameworkLabel} type reference to {reference.TypeDisplayName}.";
        var resolvedSeverity = reference is null ? IssueSeverity.Warning : severity;

        return CreateIssue(
            ruleId,
            index,
            title,
            resolvedDescription,
            resolvedSeverity,
            project,
            reference?.FilePath ?? project.FilePath,
            reference?.LineNumber,
            recommendation,
            reference is null ? IssueConfidence.Medium : IssueConfidence.High,
            reference is null ? IssueEnrichmentService.MsBuildProjectConfiguration : IssueEnrichmentService.RoslynSemanticAnalysis);
    }

    private IEnumerable<IReadOnlyList<string>> FindCycles(SolutionAnalysisContext context)
    {
        var productionProjects = context.Projects
            .Where(AnalyzerUtilities.IsProductionProject)
            .ToArray();
        var adjacency = productionProjects.ToDictionary(
            project => project.Name,
            project => _semanticHelper.GetReferencedProjects(context, project)
                .Where(AnalyzerUtilities.IsProductionProject)
                .Select(reference => reference.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);

        var cycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectName in adjacency.Keys)
        {
            Visit(projectName, new List<string>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        return cycles.Select(cycle => (IReadOnlyList<string>)cycle.Split('|'));

        void Visit(string projectName, List<string> path, HashSet<string> visiting)
        {
            if (visiting.Contains(projectName))
            {
                var startIndex = path.FindIndex(name => name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
                if (startIndex >= 0)
                {
                    cycles.Add(string.Join('|', path.Skip(startIndex).Concat([projectName])));
                }

                return;
            }

            if (!adjacency.TryGetValue(projectName, out var dependencies))
            {
                return;
            }

            visiting.Add(projectName);
            path.Add(projectName);

            foreach (var dependency in dependencies)
            {
                Visit(dependency, path, visiting);
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(projectName);
        }
    }

    private static bool ReferencesEntityFramework(SolutionAnalysisContext context, AnalyzedProject project)
    {
        return AnalyzerUtilities.ProjectHasPackage(project, "Microsoft.EntityFrameworkCore")
            || context.SemanticProjects.Any(semanticProject =>
                string.Equals(semanticProject.Project.FilePath, project.FilePath, StringComparison.OrdinalIgnoreCase)
                && semanticProject.ReferencedAssemblyNames.Any(name =>
                    name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ReferencesAspNetCore(SolutionAnalysisContext context, AnalyzedProject project)
    {
        return AnalyzerUtilities.ProjectHasPackage(project, "Microsoft.AspNetCore")
            || context.SemanticProjects.Any(semanticProject =>
                string.Equals(semanticProject.Project.FilePath, project.FilePath, StringComparison.OrdinalIgnoreCase)
                && semanticProject.ReferencedAssemblyNames.Any(name =>
                    name.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsDomainProject(AnalyzedProject project) => project.LogicalLayer == AnalyzerUtilities.DomainLayer;

    private static bool IsApplicationProject(AnalyzedProject project) => project.LogicalLayer == AnalyzerUtilities.ApplicationLayer;

    private static bool IsInfrastructureProject(AnalyzedProject project) => project.LogicalLayer == AnalyzerUtilities.InfrastructureLayer;

    private static bool IsApiOrWebProject(AnalyzedProject project)
    {
        return project.LogicalLayer == AnalyzerUtilities.PresentationLayer;
    }

    private static bool IsInfrastructureOrApiProject(AnalyzedProject project)
    {
        return IsInfrastructureProject(project) || IsApiOrWebProject(project);
    }

    private static bool IsLowerLayer(AnalyzedProject project)
    {
        return IsDomainProject(project) || IsApplicationProject(project) || IsInfrastructureProject(project);
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
            Category = AnalysisCategories.Architecture,
            ProjectName = project?.Name,
            FilePath = filePath,
            LineNumber = lineNumber,
            Recommendation = recommendation,
            Confidence = confidence ?? IssueConfidence.High,
            DetectionMethod = detectionMethod ?? (lineNumber is > 0
                ? IssueEnrichmentService.RoslynSemanticAnalysis
                : IssueEnrichmentService.MsBuildProjectConfiguration),
            WhyDetected = AnalyzerUtilities.BuildEvidence(
                ("Rule", ruleId),
                ("Project", project?.Name),
                ("File", filePath),
                ("Line", lineNumber?.ToString()),
                ("Detected", description),
                ("Applicability", "Production multi-project solution with inferred architecture layers."))
        };
    }
}
