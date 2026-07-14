using Forge.Api.Analysis;
using Forge.Api.Models;

namespace Forge.Api.Services;

public sealed class ArchitectureMapService
{
    private const string Presentation = AnalyzerUtilities.PresentationLayer;
    private const string Application = AnalyzerUtilities.ApplicationLayer;
    private const string Domain = AnalyzerUtilities.DomainLayer;
    private const string Infrastructure = AnalyzerUtilities.InfrastructureLayer;
    private const string Shared = AnalyzerUtilities.SharedLayer;
    private const string Test = AnalyzerUtilities.TestLayer;
    private const string Unknown = AnalyzerUtilities.UnknownLayer;

    private static readonly IReadOnlyDictionary<string, int> LayerOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [Presentation] = 4,
        [Infrastructure] = 3,
        [Application] = 2,
        [Domain] = 1,
        [Shared] = 0,
        [Test] = 0,
        [Unknown] = 0
    };

    public ArchitectureMap Build(
        SolutionAnalysisContext context,
        ProjectGraph projectGraph,
        IReadOnlyList<AnalysisIssue> issues)
    {
        var issueCounts = issues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.ProjectName))
            .GroupBy(issue => issue.ProjectName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var projects = projectGraph.Projects
            .Select(project =>
            {
                var analyzedProject = context.Projects.FirstOrDefault(candidate =>
                    candidate.Name.Equals(project.Name, StringComparison.OrdinalIgnoreCase));
                var projectIssues = issueCounts.GetValueOrDefault(project.Name, Array.Empty<AnalysisIssue>());

                return new ArchitectureMapProject
                {
                    Name = project.Name,
                    FilePath = project.FilePath,
                    Layer = InferLayer(project.Name, analyzedProject),
                    NamespaceRoot = InferNamespaceRoot(project.Name, analyzedProject),
                    IssueCount = projectIssues.Length,
                    CriticalOrErrorCount = projectIssues.Count(issue =>
                        issue.Severity is IssueSeverity.Critical or IssueSeverity.Error)
                };
            })
            .OrderBy(project => GetLayerOrder(project.Layer))
            .ThenBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projectByName = projects.ToDictionary(project => project.Name, StringComparer.OrdinalIgnoreCase);
        var dependencies = projectGraph.Dependencies
            .DistinctBy(dependency => $"{dependency.SourceProjectName}->{dependency.TargetProjectName}", StringComparer.OrdinalIgnoreCase)
            .Select(dependency => BuildDependency(dependency, projectByName, issues))
            .OrderBy(dependency => GetLayerOrder(dependency.SourceLayer))
            .ThenBy(dependency => dependency.SourceProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dependency => dependency.TargetProjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var dependencyViolations = dependencies
            .Where(dependency => dependency.IsViolation)
            .Select((dependency, index) => new ArchitectureMapViolation
            {
                Id = $"ARCHMAP-{index + 1:D3}",
                Title = dependency.RuleId switch
                {
                    "ARCHMAP001" => "Domain boundary violation",
                    "ARCHMAP002" => "Application depends directly on Infrastructure",
                    "ARCHMAP003" => "Infrastructure dependency risk",
                    "ARCHMAP004" => "Lower layer depends on presentation",
                    "ARCHMAP005" => "Presentation depends directly on persistence",
                    _ => "Architecture boundary risk"
                },
                Description = dependency.Reason ?? $"{dependency.SourceProjectName} references {dependency.TargetProjectName}.",
                RuleId = dependency.RuleId ?? "ARCHMAP000",
                SourceProjectName = dependency.SourceProjectName,
                TargetProjectName = dependency.TargetProjectName,
                RelatedFindingId = dependency.RelatedFindingId
            });

        var cycleViolations = FindCycles(projectGraph)
            .Select((cycle, index) => new ArchitectureMapViolation
            {
                Id = $"ARCHMAP-CYCLE-{index + 1:D3}",
                Title = "Circular project dependency",
                Description = $"The project graph contains a cycle: {string.Join(" -> ", cycle)}.",
                RuleId = "ARCHMAP006",
                SourceProjectName = cycle.FirstOrDefault(),
                TargetProjectName = cycle.Skip(1).FirstOrDefault(),
                RelatedFindingId = issues.FirstOrDefault(issue =>
                    issue.RuleId == "ARCH005"
                    && issue.Description.Contains(cycle[0], StringComparison.OrdinalIgnoreCase))?.Id
            });

        return new ArchitectureMap
        {
            Layers = projects
                .GroupBy(project => project.Layer, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => GetLayerOrder(group.Key))
                .Select(group => new ArchitectureLayer
                {
                    Name = group.Key,
                    Order = GetLayerOrder(group.Key),
                    ProjectNames = group.Select(project => project.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray()
                })
                .ToArray(),
            Projects = projects,
            Dependencies = dependencies,
            Violations = dependencyViolations.Concat(cycleViolations).ToArray()
        };
    }

    private static ArchitectureMapDependency BuildDependency(
        ProjectDependency dependency,
        IReadOnlyDictionary<string, ArchitectureMapProject> projectByName,
        IReadOnlyList<AnalysisIssue> issues)
    {
        var sourceLayer = projectByName.TryGetValue(dependency.SourceProjectName, out var sourceProject)
            ? sourceProject.Layer
            : InferLayer(dependency.SourceProjectName, null);
        var targetLayer = projectByName.TryGetValue(dependency.TargetProjectName, out var targetProject)
            ? targetProject.Layer
            : InferLayer(dependency.TargetProjectName, null);
        var rule = GetViolationRule(sourceLayer, targetLayer);
        var relatedIssue = rule.IsViolation
            ? FindRelatedArchitectureIssue(dependency, issues, rule.RuleId)
            : null;
        var isViolation = rule.IsViolation
            && (rule.RuleId != "ARCHMAP005" || relatedIssue is not null);
        var ruleId = isViolation ? rule.RuleId : null;
        var reason = isViolation
            ? rule.Reason
            : null;

        return new ArchitectureMapDependency
        {
            SourceProjectName = dependency.SourceProjectName,
            TargetProjectName = dependency.TargetProjectName,
            SourceLayer = sourceLayer,
            TargetLayer = targetLayer,
            Direction = GetDependencyDirection(sourceLayer, targetLayer),
            IsViolation = isViolation,
            RuleId = ruleId,
            Reason = reason is null
                ? $"{dependency.SourceProjectName} references {dependency.TargetProjectName} and follows the inferred layer direction."
                : $"{dependency.SourceProjectName} references {dependency.TargetProjectName}. {reason}",
            RelatedFindingId = relatedIssue?.Id
        };
    }

    private static (bool IsViolation, string? RuleId, string? Reason) GetViolationRule(string sourceLayer, string targetLayer)
    {
        if (sourceLayer == Test || targetLayer == Test)
        {
            return (false, null, null);
        }

        if (sourceLayer == Domain && targetLayer is Infrastructure or Presentation)
        {
            return (true, "ARCHMAP001", "Domain code should remain independent of infrastructure and delivery concerns.");
        }

        if (sourceLayer == Application && targetLayer == Infrastructure)
        {
            return (true, "ARCHMAP002", "Application code should depend on abstractions and let Infrastructure implement them at the composition root.");
        }

        if (sourceLayer is Domain or Application or Infrastructure && targetLayer == Presentation)
        {
            return (true, "ARCHMAP004", "API and Web projects should sit at the delivery edge, not be referenced by lower layers.");
        }

        if (sourceLayer == Presentation && targetLayer == Infrastructure)
        {
            return (true, "ARCHMAP005", "Presentation is reaching directly into persistence/infrastructure instead of depending on application use cases.");
        }

        return (false, null, null);
    }

    private static string GetDependencyDirection(string sourceLayer, string targetLayer)
    {
        var sourceOrder = GetLayerOrder(sourceLayer);
        var targetOrder = GetLayerOrder(targetLayer);

        if (sourceOrder == targetOrder)
        {
            return "Lateral";
        }

        return sourceOrder > targetOrder ? "Inward" : "Outward";
    }

    private static AnalysisIssue? FindRelatedArchitectureIssue(
        ProjectDependency dependency,
        IReadOnlyList<AnalysisIssue> issues,
        string? ruleId)
    {
        var expectedRuleId = ruleId switch
        {
            "ARCHMAP001" => "ARCH001",
            "ARCHMAP002" => "ARCH003",
            "ARCHMAP004" => "ARCH004",
            _ => null
        };

        return issues
            .Where(issue => issue.Category == AnalysisCategories.Architecture)
            .Where(issue => expectedRuleId is null || issue.RuleId == expectedRuleId)
            .OrderBy(issue => expectedRuleId is not null && issue.RuleId == expectedRuleId ? 0 : 1)
            .ThenByDescending(issue => issue.Severity)
            .FirstOrDefault(issue =>
            {
                var text = $"{issue.ProjectName} {issue.Title} {issue.Description}".ToLowerInvariant();
                return text.Contains(dependency.SourceProjectName.ToLowerInvariant())
                    && text.Contains(dependency.TargetProjectName.ToLowerInvariant());
            });
    }

    private static IReadOnlyList<IReadOnlyList<string>> FindCycles(ProjectGraph graph)
    {
        var adjacency = graph.Dependencies
            .GroupBy(dependency => dependency.SourceProjectName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(dependency => dependency.TargetProjectName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var cycles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in graph.Projects.Select(project => project.Name))
        {
            Visit(project, new List<string>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        return cycles.Values.ToArray();

        void Visit(string projectName, List<string> path, HashSet<string> visiting)
        {
            if (visiting.Contains(projectName))
            {
                var startIndex = path.FindIndex(name => name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
                if (startIndex >= 0)
                {
                    var normalizedCycle = AnalyzerUtilities.NormalizeCycle(path.Skip(startIndex).Concat([projectName]));
                    if (!string.IsNullOrWhiteSpace(normalizedCycle.Key))
                    {
                        cycles.TryAdd(normalizedCycle.Key, normalizedCycle.Cycle);
                    }
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

    private static string InferLayer(string projectName, AnalyzedProject? project)
    {
        return project?.LogicalLayer ?? AnalyzerUtilities.InferLogicalLayer(projectName, isWebProject: false, isTestProject: false);
    }

    private static string InferNamespaceRoot(string projectName, AnalyzedProject? project)
    {
        var assemblyName = project?.AssemblyName;
        var candidate = string.IsNullOrWhiteSpace(assemblyName) ? projectName : assemblyName;
        var parts = candidate.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length >= 2 ? string.Join('.', parts.Take(2)) : candidate;
    }

    private static bool HasToken(string value, string token)
    {
        return AnalyzerUtilities.HasToken(value, token);
    }

    private static int GetLayerOrder(string layer)
    {
        return LayerOrder.GetValueOrDefault(layer, 0);
    }
}
