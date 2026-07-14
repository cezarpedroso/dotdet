using System.Xml.Linq;
using System.Text;
using Forge.Api.Analysis;
using Forge.Api.Analyzers;
using Forge.Api.Models;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace Forge.Api.Services;

public sealed partial class SolutionAnalysisService
{
    public const int SourcePreviewFileCountLimit = 250;
    public const long SourcePreviewByteLimit = 5_000_000;
    private const long SourcePreviewSingleFileByteLimit = 750_000;

    private readonly ArchitectureAnalyzer _architectureAnalyzer;
    private readonly DependencyInjectionAnalyzer _dependencyInjectionAnalyzer;
    private readonly EfCoreAnalyzer _efCoreAnalyzer;
    private readonly SecurityConfigurationAnalyzer _securityConfigurationAnalyzer;
    private readonly ApiReadinessAnalyzer _apiReadinessAnalyzer;
    private readonly FindingGroupingService _findingGroupingService;
    private readonly IssueEnrichmentService _issueEnrichmentService;
    private readonly ScoringService _scoringService;
    private readonly ArchitectureMapService _architectureMapService;
    private readonly EngineeringAssessmentService _engineeringAssessmentService;
    private readonly SuppressionService _suppressionService;
    private readonly ILogger<SolutionAnalysisService> _logger;

    public SolutionAnalysisService(
        ArchitectureAnalyzer architectureAnalyzer,
        DependencyInjectionAnalyzer dependencyInjectionAnalyzer,
        EfCoreAnalyzer efCoreAnalyzer,
        SecurityConfigurationAnalyzer securityConfigurationAnalyzer,
        ApiReadinessAnalyzer apiReadinessAnalyzer,
        FindingGroupingService findingGroupingService,
        IssueEnrichmentService issueEnrichmentService,
        ScoringService scoringService,
        ArchitectureMapService architectureMapService,
        EngineeringAssessmentService engineeringAssessmentService,
        SuppressionService suppressionService,
        ILogger<SolutionAnalysisService> logger)
    {
        _architectureAnalyzer = architectureAnalyzer;
        _dependencyInjectionAnalyzer = dependencyInjectionAnalyzer;
        _efCoreAnalyzer = efCoreAnalyzer;
        _securityConfigurationAnalyzer = securityConfigurationAnalyzer;
        _apiReadinessAnalyzer = apiReadinessAnalyzer;
        _findingGroupingService = findingGroupingService;
        _issueEnrichmentService = issueEnrichmentService;
        _scoringService = scoringService;
        _architectureMapService = architectureMapService;
        _engineeringAssessmentService = engineeringAssessmentService;
        _suppressionService = suppressionService;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(string inputPath, CancellationToken cancellationToken)
    {
        return AnalyzeAsync(inputPath, AnalysisInputTrust.TrustedLocalDevelopment, cancellationToken);
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        string inputPath,
        AnalysisInputTrust inputTrust,
        CancellationToken cancellationToken)
    {
        var solutionPath = ResolveSolutionPath(inputPath);
        var options = AnalysisLoadOptions.For(inputTrust);
        var context = await BuildContextAsync(solutionPath, options, cancellationToken);
        var fidelity = DetermineAnalysisFidelity(
            options,
            context.Projects.Count,
            context.SemanticProjects.Count,
            context.SemanticDocuments.Count);

        var issues = new List<AnalysisIssue>();
        issues.AddRange(_architectureAnalyzer.Analyze(context));
        issues.AddRange(_dependencyInjectionAnalyzer.Analyze(context));
        issues.AddRange(_efCoreAnalyzer.Analyze(context));
        issues.AddRange(_securityConfigurationAnalyzer.Analyze(context));
        issues.AddRange(_apiReadinessAnalyzer.Analyze(context));
        issues = _findingGroupingService.Group(issues, context.RootDirectory).ToList();
        issues = _issueEnrichmentService.Enrich(issues).ToList();
        issues = NormalizeDetectionMethodsForFidelity(
            issues,
            context.SemanticProjects,
            context.SemanticDocuments).ToList();
        var suppressionFile = _suppressionService.Load(solutionPath);
        issues = _suppressionService.Apply(issues, solutionPath, suppressionFile).ToList();

        var orderedIssues = issues
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Category)
            .ThenBy(issue => issue.ProjectName)
            .ThenBy(issue => issue.FilePath)
            .ToArray();
        var projectGraph = BuildProjectGraph(context);
        var categoryScores = _scoringService.CalculateCategoryScores(orderedIssues);
        var overallScore = _scoringService.CalculateOverallScore(categoryScores, orderedIssues);
        var architectureMap = _architectureMapService.Build(context, projectGraph, orderedIssues);
        var sourcePreview = await BuildAnalysisSourceFilesAsync(context, orderedIssues, cancellationToken);
        var sourceFiles = sourcePreview.Files;

        return new AnalysisResult
        {
            SolutionName = context.SolutionName,
            AnalyzedAt = DateTimeOffset.UtcNow,
            OverallScore = overallScore,
            CategoryScores = categoryScores,
            Issues = orderedIssues,
            ProjectGraph = projectGraph,
            SourceFiles = sourceFiles,
            SourcePreviewAvailable = sourceFiles.Count > 0,
            SourcePreviewUnavailableReason = sourceFiles.Count > 0
                ? null
                : "Source preview is unavailable for this analysis result.",
            SourcePreviewCapped = sourcePreview.OmittedFileCount > 0,
            SourcePreviewCappedReason = sourcePreview.OmittedFileCount > 0
                ? $"Source preview was limited to {SourcePreviewFileCountLimit} files and {SourcePreviewByteLimit / 1_000_000} MB. {sourcePreview.OmittedFileCount} additional file(s) were omitted."
                : null,
            SourcePreviewIncludedFileCount = sourceFiles.Count,
            SourcePreviewOmittedFileCount = sourcePreview.OmittedFileCount,
            SourcePreviewIncludedBytes = sourcePreview.IncludedBytes,
            SourcePreviewFileCountLimit = SourcePreviewFileCountLimit,
            SourcePreviewByteLimit = SourcePreviewByteLimit,
            AnalysisFidelity = fidelity.AnalysisFidelity,
            SemanticAnalysisSkipped = fidelity.SemanticAnalysisSkipped,
            SemanticAnalysisSkippedReason = fidelity.SemanticAnalysisSkippedReason,
            ArchitectureMap = architectureMap,
            EngineeringAssessment = _engineeringAssessmentService.Build(overallScore, categoryScores, orderedIssues, architectureMap),
            SolutionPath = solutionPath,
            RepositoryRoot = context.RootDirectory,
            SuppressionFilePath = _suppressionService.GetSuppressionFilePath(solutionPath),
            SuppressionCount = suppressionFile.Suppressions.Count
        };
    }

    public static string ResolveSolutionPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("A solution path is required.", nameof(inputPath));
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(inputPath.Trim('"', ' '));
        var fullPath = AnalyzerUtilities.NormalizePath(expandedPath);

        if (File.Exists(fullPath))
        {
            var extension = Path.GetExtension(fullPath);
            if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            throw new ArgumentException("The path must point to a .sln/.slnx file or a directory containing one.");
        }

        if (!Directory.Exists(fullPath))
        {
            throw new FileNotFoundException($"Could not find '{inputPath}'.");
        }

        var candidates = Directory
            .EnumerateFiles(fullPath, "*.sln", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(fullPath, "*.slnx", SearchOption.AllDirectories))
            .Where(path => !AnalyzerUtilities.IsUnderBuildOutput(path))
            .OrderBy(path => path.Count(character => character == Path.DirectorySeparatorChar))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new FileNotFoundException($"No .sln or .slnx file was found under '{inputPath}'.");
        }

        return AnalyzerUtilities.NormalizePath(candidates[0]);
    }

    public static string ResolveSampleSolutionPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "samples",
                "Forge.SampleShop",
                "Forge.SampleShop.slnx");

            if (File.Exists(candidate))
            {
                return AnalyzerUtilities.NormalizePath(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("The bundled sample solution could not be found.");
    }

    private async Task<SolutionAnalysisContext> BuildContextAsync(
        string solutionPath,
        AnalysisLoadOptions options,
        CancellationToken cancellationToken)
    {
        var rootDirectory = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
        var projectDefinitions = options.AllowMSBuildWorkspace
            ? await TryLoadProjectsWithMsBuildAsync(solutionPath, cancellationToken)
            : new List<ProjectDefinition>();

        if (projectDefinitions.Count == 0)
        {
            projectDefinitions = LoadProjectDefinitionsFromSeeds(
                DiscoverProjectFilesFromSolution(solutionPath),
                options.AllowMSBuildEvaluation);
        }

        if (projectDefinitions.Count == 0)
        {
            var discoveredProjects = Directory
                .EnumerateFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !AnalyzerUtilities.IsUnderBuildOutput(path))
                .ToList();

            projectDefinitions = LoadProjectDefinitionsFromSeeds(
                discoveredProjects
                    .Select(path => new ProjectDefinitionSeed(Path.GetFileNameWithoutExtension(path), AnalyzerUtilities.NormalizePath(path)))
                    .ToList(),
                options.AllowMSBuildEvaluation);
        }

        var projects = BuildAnalyzedProjects(projectDefinitions)
            .GroupBy(project => AnalyzerUtilities.NormalizePath(project.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sourceFiles = await LoadSourceFilesAsync(projects, cancellationToken);
        var semanticContext = await LoadSemanticContextAsync(solutionPath, projects, options, cancellationToken);
        var appSettingsFiles = Directory
            .EnumerateFiles(rootDirectory, "appsettings*.json", SearchOption.AllDirectories)
            .Where(path => !AnalyzerUtilities.IsUnderBuildOutput(path))
            .Select(AnalyzerUtilities.NormalizePath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SolutionAnalysisContext
        {
            SolutionPath = solutionPath,
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
            RootDirectory = rootDirectory,
            Projects = projects,
            SourceFiles = sourceFiles,
            SemanticProjects = semanticContext.Projects,
            SemanticDocuments = semanticContext.Documents,
            AppSettingsFiles = appSettingsFiles
        };
    }

    private async Task<List<ProjectDefinition>> TryLoadProjectsWithMsBuildAsync(
        string solutionPath,
        CancellationToken cancellationToken)
    {
        try
        {
            MsBuildRegistration.EnsureRegistered();
            using var workspace = CreateWorkspace(out var diagnostics);
            var seeds = DiscoverProjectFilesFromSolution(solutionPath);
            IReadOnlyList<Microsoft.CodeAnalysis.Project> roslynProjects;

            if (Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                roslynProjects = await OpenProjectsAsync(workspace, seeds, cancellationToken);
            }
            else
            {
                var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
                roslynProjects = solution.Projects
                    .Where(project => !string.IsNullOrWhiteSpace(project.FilePath))
                    .ToArray();
            }

            LogWorkspaceDiagnostics(diagnostics, solutionPath);
            _logger.LogInformation(
                "Loaded {ProjectCount} Roslyn project(s) for project-definition analysis from {SolutionPath}.",
                roslynProjects.Count,
                solutionPath);

            using var projectCollection = CreateProjectCollection();
            return roslynProjects
                .Where(project => !string.IsNullOrWhiteSpace(project.FilePath))
                .Select(project => BuildProjectDefinition(
                    new ProjectDefinitionSeed(project.Name, AnalyzerUtilities.NormalizePath(project.FilePath!)),
                    projectCollection,
                    assemblyNameHint: project.AssemblyName))
                .ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogInformation(exception, "MSBuildWorkspace could not load {SolutionPath}; falling back to project discovery.", solutionPath);
            return new List<ProjectDefinition>();
        }
    }

    private static List<ProjectDefinitionSeed> DiscoverProjectFilesFromSolution(string solutionPath)
    {
        return Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            ? DiscoverProjectFilesFromSlnx(solutionPath)
            : DiscoverProjectFilesFromSln(solutionPath);
    }

    private static List<ProjectDefinitionSeed> DiscoverProjectFilesFromSlnx(string solutionPath)
    {
        var rootDirectory = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
        var document = XDocument.Load(solutionPath);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => AnalyzerUtilities.NormalizePath(Path.Combine(rootDirectory, value!)))
            .Where(File.Exists)
            .Select(path => new ProjectDefinitionSeed(Path.GetFileNameWithoutExtension(path), path))
            .ToList();
    }

    private static List<ProjectDefinitionSeed> DiscoverProjectFilesFromSln(string solutionPath)
    {
        var solutionFile = SolutionFile.Parse(solutionPath);

        return solutionFile.ProjectsInOrder
            .Where(project => !string.IsNullOrWhiteSpace(project.AbsolutePath))
            .Where(project => project.AbsolutePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(project => AnalyzerUtilities.NormalizePath(project.AbsolutePath))
            .Where(File.Exists)
            .Select(path => new ProjectDefinitionSeed(Path.GetFileNameWithoutExtension(path), path))
            .DistinctBy(project => project.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<ProjectDefinition> LoadProjectDefinitionsFromSeeds(
        IReadOnlyList<ProjectDefinitionSeed> seeds,
        bool allowMSBuildEvaluation)
    {
        if (seeds.Count == 0)
        {
            return new List<ProjectDefinition>();
        }

        if (!allowMSBuildEvaluation)
        {
            return seeds
                .Select(seed => BuildProjectDefinitionFromXml(seed, assemblyNameHint: null))
                .ToList();
        }

        MsBuildRegistration.EnsureRegistered();
        using var projectCollection = CreateProjectCollection();

        return seeds
            .Select(seed => BuildProjectDefinition(seed, projectCollection))
            .ToList();
    }

    private ProjectDefinition BuildProjectDefinition(
        ProjectDefinitionSeed seed,
        ProjectCollection projectCollection,
        string? assemblyNameHint = null)
    {
        try
        {
            var globalProperties = CreateMsBuildGlobalProperties();
            var evaluatedProject = new Microsoft.Build.Evaluation.Project(seed.FilePath, globalProperties, toolsVersion: null, projectCollection);
            var packageReferences = evaluatedProject.Items
                .Where(item => item.ItemType.Equals("PackageReference", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.EvaluatedInclude)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var projectReferences = evaluatedProject.Items
                .Where(item => item.ItemType.Equals("ProjectReference", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.EvaluatedInclude)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => AnalyzerUtilities.NormalizePath(Path.Combine(seed.DirectoryPath, value!)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var sdk = evaluatedProject.Xml.Sdk;
            var targetFramework = FirstNonEmpty(
                evaluatedProject.GetPropertyValue("TargetFramework"),
                evaluatedProject.GetPropertyValue("TargetFrameworks"));
            var assemblyName = FirstNonEmpty(
                evaluatedProject.GetPropertyValue("AssemblyName"),
                assemblyNameHint,
                Path.GetFileNameWithoutExtension(seed.FilePath));

            return new ProjectDefinition(
                seed.Name,
                seed.FilePath,
                assemblyName,
                sdk,
                targetFramework,
                packageReferences,
                projectReferences,
                true);
        }
        catch (Exception exception)
        {
            _logger.LogInformation(exception, "MSBuild evaluation could not fully resolve {ProjectPath}; falling back to XML parsing.", seed.FilePath);
            return BuildProjectDefinitionFromXml(seed, assemblyNameHint);
        }
    }

    private static ProjectDefinition BuildProjectDefinitionFromXml(
        ProjectDefinitionSeed seed,
        string? assemblyNameHint)
    {
        var packageReferences = AnalyzerUtilities.ReadPackageReferences(seed.FilePath);
        var projectReferences = AnalyzerUtilities.ReadProjectReferences(seed.FilePath);
        var projectDocument = XDocument.Load(seed.FilePath);
        var sdk = projectDocument.Root?.Attribute("Sdk")?.Value;

        return new ProjectDefinition(
            seed.Name,
            seed.FilePath,
            assemblyNameHint ?? Path.GetFileNameWithoutExtension(seed.FilePath),
            sdk,
            projectDocument.Descendants().FirstOrDefault(element => element.Name.LocalName == "TargetFramework")?.Value
                ?? projectDocument.Descendants().FirstOrDefault(element => element.Name.LocalName == "TargetFrameworks")?.Value,
            packageReferences,
            projectReferences,
            false);
    }

    private static IReadOnlyList<AnalyzedProject> BuildAnalyzedProjects(IReadOnlyList<ProjectDefinition> definitions)
    {
        var projects = definitions
            .Select(definition =>
            {
                var isTestProject = IsTestProject(definition);
                var isWebProject = IsWebProject(definition);
                var isEntryPointProject = IsAspNetCoreEntryPointProject(definition);

                return new AnalyzedProject
                {
                    Name = definition.Name,
                    FilePath = definition.FilePath,
                    AssemblyName = definition.AssemblyName,
                    DirectoryPath = Path.GetDirectoryName(definition.FilePath) ?? Directory.GetCurrentDirectory(),
                    Sdk = definition.Sdk,
                    TargetFramework = definition.TargetFramework,
                    IsWebProject = isWebProject,
                    IsAspNetCoreEntryPoint = isEntryPointProject,
                    IsTestProject = isTestProject,
                    LogicalLayer = AnalyzerUtilities.InferLogicalLayer(definition.Name, isWebProject, isTestProject),
                    LoadedWithMsBuild = definition.LoadedWithMsBuild,
                    PackageReferences = definition.PackageReferences,
                    ProjectReferencePaths = definition.ProjectReferencePaths,
                    TransitiveProjectReferencePaths = Array.Empty<string>()
                };
            })
            .ToArray();

        var projectByPath = projects.ToDictionary(
            project => AnalyzerUtilities.NormalizePath(project.FilePath),
            StringComparer.OrdinalIgnoreCase);

        return projects
            .Select(project => project with
            {
                TransitiveProjectReferencePaths = GetTransitiveProjectReferencePaths(project, projectByPath)
            })
            .ToArray();
    }

    private static IReadOnlyList<string> GetTransitiveProjectReferencePaths(
        AnalyzedProject project,
        IReadOnlyDictionary<string, AnalyzedProject> projectByPath)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toVisit = new Stack<string>(project.ProjectReferencePaths.Reverse());

        while (toVisit.Count > 0)
        {
            var next = AnalyzerUtilities.NormalizePath(toVisit.Pop());
            if (!visited.Add(next))
            {
                continue;
            }

            if (!projectByPath.TryGetValue(next, out var referencedProject))
            {
                continue;
            }

            foreach (var transitiveReference in referencedProject.ProjectReferencePaths)
            {
                toVisit.Push(transitiveReference);
            }
        }

        return visited.ToArray();
    }

    private static bool IsWebProject(ProjectDefinition project)
    {
        return project.Sdk?.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) == true
            || AnalyzerUtilities.HasToken(project.Name, "Web")
            || AnalyzerUtilities.HasToken(project.Name, "Api")
            || AnalyzerUtilities.HasToken(project.Name, "PublicApi")
            || AnalyzerUtilities.HasToken(project.Name, "BlazorAdmin")
            || IsAspNetCoreEntryPointProject(project);
    }

    private static bool IsTestProject(ProjectDefinition project)
    {
        return AnalyzerUtilities.IsTestProjectName(project.Name)
            || AnalyzerUtilities.HasTestFrameworkReference(project.PackageReferences);
    }

    private static bool IsAspNetCoreEntryPointProject(ProjectDefinition project)
    {
        if (project.Sdk?.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        var projectDirectory = Path.GetDirectoryName(project.FilePath) ?? Directory.GetCurrentDirectory();
        var startupFiles = new[]
            {
                Path.Combine(projectDirectory, "Program.cs"),
                Path.Combine(projectDirectory, "Startup.cs")
            }
            .Where(File.Exists)
            .ToArray();

        if (startupFiles.Length == 0)
        {
            return false;
        }

        return startupFiles.Any(file =>
        {
            var text = File.ReadAllText(file);
            return text.Contains("WebApplication.CreateBuilder", StringComparison.Ordinal)
                || text.Contains("CreateHostBuilder", StringComparison.Ordinal)
                || text.Contains("IApplicationBuilder", StringComparison.Ordinal)
                || text.Contains("MapControllers", StringComparison.Ordinal)
                || text.Contains("MapGet", StringComparison.Ordinal)
                || text.Contains("UseRouting", StringComparison.Ordinal);
        });
    }

    private static ProjectCollection CreateProjectCollection()
    {
        return new ProjectCollection(CreateMsBuildGlobalProperties());
    }

    private static Dictionary<string, string> CreateMsBuildGlobalProperties()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DesignTimeBuild"] = "true",
            ["BuildProjectReferences"] = "false",
            ["SkipCompilerExecution"] = "true"
        };
    }

    private static MSBuildWorkspace CreateWorkspace(out List<WorkspaceDiagnostic> diagnostics)
    {
        var workspaceDiagnostics = new List<WorkspaceDiagnostic>();
        diagnostics = workspaceDiagnostics;
        var workspace = MSBuildWorkspace.Create(CreateMsBuildGlobalProperties());
        workspace.RegisterWorkspaceFailedHandler(args => workspaceDiagnostics.Add(args.Diagnostic));
        return workspace;
    }

    private void LogWorkspaceDiagnostics(IEnumerable<WorkspaceDiagnostic> diagnostics, string inputPath)
    {
        foreach (var diagnostic in diagnostics
                     .GroupBy(diagnostic => $"{diagnostic.Kind}:{diagnostic.Message}", StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            _logger.LogInformation(
                "MSBuildWorkspace reported {DiagnosticKind} while loading {InputPath}: {Message}",
                diagnostic.Kind,
                inputPath,
                diagnostic.Message);
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static async Task<IReadOnlyList<SourceFileContext>> LoadSourceFilesAsync(
        IReadOnlyList<AnalyzedProject> projects,
        CancellationToken cancellationToken)
    {
        var sourceFiles = new List<SourceFileContext>();

        foreach (var project in projects)
        {
            var files = Directory
                .EnumerateFiles(project.DirectoryPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => !AnalyzerUtilities.IsUnderBuildOutput(path))
                .Where(path => !path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var text = await File.ReadAllTextAsync(file, cancellationToken);
                var syntaxTree = CSharpSyntaxTree.ParseText(text, path: file, cancellationToken: cancellationToken);
                var root = (CompilationUnitSyntax)await syntaxTree.GetRootAsync(cancellationToken);

                sourceFiles.Add(new SourceFileContext
                {
                    Project = project,
                    FilePath = AnalyzerUtilities.NormalizePath(file),
                    Text = text,
                    Root = root
                });
            }
        }

        return sourceFiles;
    }

    private static async Task<SourcePreviewBuildResult> BuildAnalysisSourceFilesAsync(
        SolutionAnalysisContext context,
        IReadOnlyList<AnalysisIssue> issues,
        CancellationToken cancellationToken)
    {
        var rootDirectory = AnalyzerUtilities.NormalizePath(context.RootDirectory);
        var projectByPath = context.Projects
            .ToDictionary(project => AnalyzerUtilities.NormalizePath(project.FilePath), StringComparer.OrdinalIgnoreCase);
        var projectsByDirectory = context.Projects
            .OrderByDescending(project => project.DirectoryPath.Length)
            .ToArray();
        var candidatePaths = issues
            .Select(issue => issue.FilePath)
            .OfType<string>()
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Concat(context.SourceFiles.Select(file => file.FilePath))
            .Concat(context.AppSettingsFiles)
            .Concat(context.Projects.Select(project => project.FilePath))
            .Concat(DiscoverSourcePreviewFiles(rootDirectory))
            .Select(AnalyzerUtilities.NormalizePath)
            .Where(path => IsSourceProjectionCandidate(path, rootDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sourceFiles = new List<AnalysisSourceFile>();
        long includedBytes = 0;
        var omittedFileCount = 0;

        foreach (var path in candidatePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length > SourcePreviewSingleFileByteLimit)
            {
                if (fileInfo.Exists)
                {
                    omittedFileCount++;
                }
                continue;
            }

            if (sourceFiles.Count >= SourcePreviewFileCountLimit)
            {
                omittedFileCount++;
                continue;
            }

            var content = await File.ReadAllTextAsync(path, cancellationToken);
            var contentBytes = Encoding.UTF8.GetByteCount(content);
            if (includedBytes + contentBytes > SourcePreviewByteLimit)
            {
                omittedFileCount++;
                continue;
            }

            var relativePath = Path.GetRelativePath(rootDirectory, path).Replace('\\', '/');

            sourceFiles.Add(new AnalysisSourceFile
            {
                ProjectName = ResolveProjectName(path, projectByPath, projectsByDirectory),
                FilePath = path,
                RelativePath = relativePath,
                Content = content,
                Language = GetLanguage(path)
            });
            includedBytes += contentBytes;
        }

        return new SourcePreviewBuildResult(sourceFiles, includedBytes, omittedFileCount);
    }

    private sealed record SourcePreviewBuildResult(
        IReadOnlyList<AnalysisSourceFile> Files,
        long IncludedBytes,
        int OmittedFileCount);

    private static IEnumerable<string> DiscoverSourcePreviewFiles(string rootDirectory)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootDirectory);

        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();

            IEnumerable<string> childDirectories;
            IEnumerable<string> childFiles;
            try
            {
                childDirectories = Directory.EnumerateDirectories(currentDirectory).ToArray();
                childFiles = Directory.EnumerateFiles(currentDirectory).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var directory in childDirectories)
            {
                if (!IsExcludedSourcePreviewDirectory(directory))
                {
                    pendingDirectories.Push(directory);
                }
            }

            foreach (var file in childFiles)
            {
                if (IsSourceProjectionCandidate(file, rootDirectory))
                {
                    yield return file;
                }
            }
        }
    }

    private static bool IsSourceProjectionCandidate(string path, string rootDirectory)
    {
        if (!File.Exists(path)
            || AnalyzerUtilities.IsUnderBuildOutput(path)
            || IsUnderExcludedSourcePreviewDirectory(path)
            || !IsUnderRoot(path, rootDirectory))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || (extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                && IsSupportedSourcePreviewJson(fileName))
            || (extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
                && fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSupportedSourcePreviewJson(string fileName)
    {
        return fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("global.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("launchSettings.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderExcludedSourcePreviewDirectory(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(IsExcludedSourcePreviewDirectoryName);
    }

    private static bool IsExcludedSourcePreviewDirectory(string directory)
    {
        return IsExcludedSourcePreviewDirectoryName(Path.GetFileName(directory));
    }

    private static bool IsExcludedSourcePreviewDirectoryName(string? directoryName)
    {
        return directoryName is not null
            && (directoryName.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || directoryName.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || directoryName.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || directoryName.Equals("node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveProjectName(
        string path,
        IReadOnlyDictionary<string, AnalyzedProject> projectByPath,
        IReadOnlyList<AnalyzedProject> projectsByDirectory)
    {
        if (projectByPath.TryGetValue(path, out var projectFile))
        {
            return projectFile.Name;
        }

        return projectsByDirectory
            .FirstOrDefault(project => path.StartsWith(project.DirectoryPath, StringComparison.OrdinalIgnoreCase))
            ?.Name
            ?? "Solution";
    }

    private static string GetLanguage(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".csproj" or ".props" or ".targets" or ".slnx" => "xml",
            ".json" => "json",
            ".md" => "markdown",
            _ => "plaintext"
        };
    }

    private static bool IsUnderRoot(string path, string rootDirectory)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, path);
        return relativePath == "."
            || (!relativePath.StartsWith("..", StringComparison.Ordinal)
                && !Path.IsPathRooted(relativePath));
    }

    private static IEnumerable<AnalysisIssue> NormalizeDetectionMethodsForFidelity(
        IEnumerable<AnalysisIssue> issues,
        IReadOnlyList<SemanticProjectContext> semanticProjects,
        IReadOnlyList<SemanticDocumentContext> semanticDocuments)
    {
        var semanticProjectNames = semanticProjects
            .Select(project => project.Project.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var semanticFilePaths = semanticDocuments
            .Select(document => AnalyzerUtilities.NormalizePath(document.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return issues.Select(issue => issue.DetectionMethod == IssueEnrichmentService.RoslynSemanticAnalysis
            && !HasSemanticCoverage(issue, semanticProjectNames, semanticFilePaths)
            ? issue with { DetectionMethod = IssueEnrichmentService.RoslynSyntaxAnalysis }
            : issue);
    }

    private static bool HasSemanticCoverage(
        AnalysisIssue issue,
        IReadOnlySet<string> semanticProjectNames,
        IReadOnlySet<string> semanticFilePaths)
    {
        return (!string.IsNullOrWhiteSpace(issue.ProjectName) && semanticProjectNames.Contains(issue.ProjectName))
            || (!string.IsNullOrWhiteSpace(issue.FilePath)
                && semanticFilePaths.Contains(AnalyzerUtilities.NormalizePath(issue.FilePath)));
    }

    public static AnalysisFidelityMetadata DetermineAnalysisFidelity(
        AnalysisLoadOptions options,
        int projectCount,
        int semanticProjectCount,
        int semanticDocumentCount)
    {
        if (!options.AllowMSBuildWorkspace)
        {
            return new AnalysisFidelityMetadata(
                "Safe Syntax Analysis",
                true,
                "Semantic project loading was skipped for untrusted input because isolated analysis is not configured. DotDet used safe syntax-based analysis.",
                false);
        }

        var hasSemanticData = semanticProjectCount > 0 && semanticDocumentCount > 0;
        var hasFullSemanticCoverage = hasSemanticData
            && projectCount > 0
            && semanticProjectCount >= projectCount;

        if (hasFullSemanticCoverage)
        {
            return new AnalysisFidelityMetadata("Roslyn Semantic Analysis", false, null, true);
        }

        if (hasSemanticData)
        {
            return new AnalysisFidelityMetadata(
                "Project Load Degraded",
                true,
                $"Roslyn semantic loading succeeded for {semanticProjectCount} of {projectCount} project(s). DotDet used project-file and syntax fallback analysis for the remaining projects.",
                true);
        }

        return new AnalysisFidelityMetadata(
            "Syntax Fallback",
            true,
            "Roslyn semantic project loading was attempted but did not produce semantic projects and documents. DotDet used project-file and syntax fallback analysis.",
            false);
    }

    private async Task<SemanticContextLoadResult> LoadSemanticContextAsync(
        string solutionPath,
        IReadOnlyList<AnalyzedProject> projects,
        AnalysisLoadOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.AllowMSBuildWorkspace)
        {
            _logger.LogInformation(
                "Skipping MSBuildWorkspace semantic load for untrusted input {SolutionPath}; using syntax-based analysis.",
                solutionPath);
            return new SemanticContextLoadResult(Array.Empty<SemanticProjectContext>(), Array.Empty<SemanticDocumentContext>());
        }

        try
        {
            MsBuildRegistration.EnsureRegistered();
            using var workspace = CreateWorkspace(out var diagnostics);
            var seeds = DiscoverProjectFilesFromSolution(solutionPath);
            Solution? solution = null;
            IReadOnlyList<Microsoft.CodeAnalysis.Project> roslynProjects;

            if (Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                roslynProjects = await OpenProjectsAsync(workspace, seeds, cancellationToken);
            }
            else
            {
                solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
                roslynProjects = solution.Projects
                    .Where(project => !string.IsNullOrWhiteSpace(project.FilePath))
                    .ToArray();
            }

            LogWorkspaceDiagnostics(diagnostics, solutionPath);
            _logger.LogInformation(
                "Loaded {ProjectCount} Roslyn project(s) for semantic analysis from {SolutionPath}.",
                roslynProjects.Count,
                solutionPath);
            var projectByPath = projects.ToDictionary(
                project => AnalyzerUtilities.NormalizePath(project.FilePath),
                StringComparer.OrdinalIgnoreCase);
            return await BuildSemanticContextResultAsync(
                roslynProjects,
                solution,
                projectByPath,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogInformation(exception, "Semantic Roslyn solution load failed for {SolutionPath}; attempting project-by-project fallback.", solutionPath);
            return await LoadSemanticProjectsIndividuallyAsync(projects, cancellationToken);
        }
    }

    private async Task<SemanticContextLoadResult> LoadSemanticProjectsIndividuallyAsync(
        IReadOnlyList<AnalyzedProject> projects,
        CancellationToken cancellationToken)
    {
        try
        {
            using var workspace = CreateWorkspace(out var diagnostics);
            var projectByPath = projects.ToDictionary(
                project => AnalyzerUtilities.NormalizePath(project.FilePath),
                StringComparer.OrdinalIgnoreCase);
            var roslynProjects = await OpenProjectsAsync(
                workspace,
                projects.Select(project => new ProjectDefinitionSeed(project.Name, project.FilePath)).ToArray(),
                cancellationToken);

            LogWorkspaceDiagnostics(diagnostics, "project-by-project semantic load");
            return await BuildSemanticContextResultAsync(roslynProjects, solution: null, projectByPath, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogInformation(exception, "Project-by-project semantic Roslyn load failed; using syntax-only analysis.");
            return new SemanticContextLoadResult(Array.Empty<SemanticProjectContext>(), Array.Empty<SemanticDocumentContext>());
        }
    }

    private static async Task<SemanticContextLoadResult> BuildSemanticContextResultAsync(
        IReadOnlyList<Microsoft.CodeAnalysis.Project> roslynProjects,
        Solution? solution,
        IReadOnlyDictionary<string, AnalyzedProject> projectByPath,
        CancellationToken cancellationToken)
    {
        var semanticProjects = new List<SemanticProjectContext>();
        var semanticDocuments = new List<SemanticDocumentContext>();

        foreach (var roslynProject in roslynProjects)
        {
            var projectPath = AnalyzerUtilities.NormalizePath(roslynProject.FilePath!);
            if (!projectByPath.TryGetValue(projectPath, out var analyzedProject))
            {
                continue;
            }

            var compilation = await roslynProject.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            var projectDocuments = new List<SemanticDocumentContext>();

            foreach (var document in roslynProject.Documents.Where(document => !string.IsNullOrWhiteSpace(document.FilePath)))
            {
                var filePath = AnalyzerUtilities.NormalizePath(document.FilePath!);
                if (AnalyzerUtilities.IsUnderBuildOutput(filePath)
                    || filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
                if (root is not CompilationUnitSyntax compilationUnit || syntaxTree is null)
                {
                    continue;
                }

                var semanticDocument = new SemanticDocumentContext
                {
                    Project = analyzedProject,
                    FilePath = filePath,
                    Root = compilationUnit,
                    SemanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true)
                };

                projectDocuments.Add(semanticDocument);
                semanticDocuments.Add(semanticDocument);
            }

            var referencedProjectPaths = roslynProject.ProjectReferences
                .Select(reference => solution?.GetProject(reference.ProjectId)?.FilePath ?? roslynProjects.FirstOrDefault(project => project.Id == reference.ProjectId)?.FilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => AnalyzerUtilities.NormalizePath(path!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            semanticProjects.Add(new SemanticProjectContext
            {
                Project = analyzedProject,
                AssemblyName = compilation.AssemblyName ?? roslynProject.AssemblyName ?? analyzedProject.AssemblyName ?? analyzedProject.Name,
                Compilation = compilation,
                Documents = projectDocuments,
                ProjectReferencePaths = referencedProjectPaths.Length > 0 ? referencedProjectPaths : analyzedProject.ProjectReferencePaths,
                ProjectReferenceNames = referencedProjectPaths
                    .Where(projectByPath.ContainsKey)
                    .Select(path => projectByPath[path].Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ReferencedAssemblyNames = compilation.ReferencedAssemblyNames
                    .Select(reference => reference.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()!
            });
        }

        return new SemanticContextLoadResult(semanticProjects, semanticDocuments);
    }

    private async Task<IReadOnlyList<Microsoft.CodeAnalysis.Project>> OpenProjectsAsync(
        MSBuildWorkspace workspace,
        IReadOnlyList<ProjectDefinitionSeed> seeds,
        CancellationToken cancellationToken)
    {
        var roslynProjects = new List<Microsoft.CodeAnalysis.Project>();

        foreach (var seed in seeds
                     .DistinctBy(project => project.FilePath, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                roslynProjects.Add(await workspace.OpenProjectAsync(seed.FilePath, cancellationToken: cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogInformation(exception, "MSBuildWorkspace could not open project {ProjectPath}.", seed.FilePath);
            }
        }

        var workspaceProjects = workspace.CurrentSolution.Projects
            .Where(project => !string.IsNullOrWhiteSpace(project.FilePath))
            .ToArray();

        return workspaceProjects.Length > 0
            ? workspaceProjects
            : roslynProjects;
    }

    private static ProjectGraph BuildProjectGraph(SolutionAnalysisContext context)
    {
        var referencePathsByProject = context.SemanticProjects
            .ToDictionary(
                project => AnalyzerUtilities.NormalizePath(project.Project.FilePath),
                project => project.ProjectReferencePaths,
                StringComparer.OrdinalIgnoreCase);
        var projectByPath = context.Projects.ToDictionary(
            project => AnalyzerUtilities.NormalizePath(project.FilePath),
            StringComparer.OrdinalIgnoreCase);

        var dependencies = context.Projects
            .SelectMany(project =>
            {
                var projectPath = AnalyzerUtilities.NormalizePath(project.FilePath);
                var references = referencePathsByProject.GetValueOrDefault(projectPath, project.ProjectReferencePaths);
                return references.Select(reference => (Project: project, Reference: reference));
            })
            .Where(edge => projectByPath.ContainsKey(edge.Reference))
            .Select(edge => new ProjectDependency
            {
                SourceProjectName = edge.Project.Name,
                TargetProjectName = projectByPath[edge.Reference].Name
            })
            .Distinct()
            .OrderBy(edge => edge.SourceProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.TargetProjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProjectGraph
        {
            Projects = context.Projects
                .Select(project => new ProjectNode
                {
                    Name = project.Name,
                    FilePath = project.FilePath,
                    LogicalLayer = project.LogicalLayer,
                    IsTestProject = project.IsTestProject,
                    IsAspNetCoreEntryPoint = project.IsAspNetCoreEntryPoint
                })
                .ToArray(),
            Dependencies = dependencies
        };
    }

    private sealed record ProjectDefinition(
        string Name,
        string FilePath,
        string? AssemblyName,
        string? Sdk,
        string? TargetFramework,
        IReadOnlySet<string> PackageReferences,
        IReadOnlyList<string> ProjectReferencePaths,
        bool LoadedWithMsBuild);

    private sealed record ProjectDefinitionSeed(string Name, string FilePath)
    {
        public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? Directory.GetCurrentDirectory();
    }

    private sealed record SemanticContextLoadResult(
        IReadOnlyList<SemanticProjectContext> Projects,
        IReadOnlyList<SemanticDocumentContext> Documents);
}

public enum AnalysisInputTrust
{
    TrustedLocalDevelopment,
    UntrustedArchive
}

public sealed record AnalysisLoadOptions(bool AllowMSBuildWorkspace, bool AllowMSBuildEvaluation)
{
    public static AnalysisLoadOptions For(AnalysisInputTrust inputTrust)
    {
        return inputTrust == AnalysisInputTrust.TrustedLocalDevelopment
            ? new AnalysisLoadOptions(AllowMSBuildWorkspace: true, AllowMSBuildEvaluation: true)
            : new AnalysisLoadOptions(AllowMSBuildWorkspace: false, AllowMSBuildEvaluation: false);
    }
}

public sealed record AnalysisFidelityMetadata(
    string AnalysisFidelity,
    bool SemanticAnalysisSkipped,
    string? SemanticAnalysisSkippedReason,
    bool HasSemanticData);
