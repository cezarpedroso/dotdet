using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Forge.Api.Analysis;

public static class AnalyzerUtilities
{
    public const string PresentationLayer = "Presentation";
    public const string ApplicationLayer = "Application";
    public const string DomainLayer = "Domain";
    public const string InfrastructureLayer = "Infrastructure";
    public const string SharedLayer = "Shared";
    public const string TestLayer = "Test";
    public const string UnknownLayer = "Unknown";

    private static readonly HashSet<string> PrimitiveOrFrameworkServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "bool",
        "byte",
        "char",
        "decimal",
        "double",
        "float",
        "Guid",
        "HttpClient",
        "IConfiguration",
        "IHostEnvironment",
        "IHttpContextAccessor",
        "ILogger",
        "IMapper",
        "IOptions",
        "IOptionsMonitor",
        "IOptionsSnapshot",
        "IServiceProvider",
        "IWebHostEnvironment",
        "int",
        "long",
        "short",
        "string",
        "TimeProvider",
        "CancellationToken",
        "DbContextOptions",
    };

    private static readonly string[] TestNameTokens =
    [
        "Tests",
        "UnitTests",
        "IntegrationTests",
        "FunctionalTests"
    ];

    private static readonly string[] TestPackageMarkers =
    [
        "Microsoft.NET.Test.Sdk",
        "xunit",
        "NUnit",
        "MSTest.TestFramework",
        "MSTest",
        "coverlet",
        "FluentAssertions",
        "Shouldly"
    ];

    public static int GetLineNumber(SyntaxNode node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        return lineSpan.StartLinePosition.Line + 1;
    }

    public static string GetTypeIdentifier(TypeSyntax type)
    {
        return type switch
        {
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            QualifiedNameSyntax qualifiedName => GetTypeIdentifier(qualifiedName.Right),
            AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText,
            NullableTypeSyntax nullableType => GetTypeIdentifier(nullableType.ElementType),
            ArrayTypeSyntax arrayType => GetTypeIdentifier(arrayType.ElementType),
            PredefinedTypeSyntax predefinedType => predefinedType.Keyword.ValueText,
            _ => type.ToString().Split('.', '<', '?').LastOrDefault() ?? type.ToString()
        };
    }

    public static bool IsLikelyFrameworkService(string typeName)
    {
        if (PrimitiveOrFrameworkServices.Contains(typeName))
        {
            return true;
        }

        return typeName.StartsWith("IEnumerable", StringComparison.OrdinalIgnoreCase)
            || typeName.StartsWith("Func", StringComparison.OrdinalIgnoreCase)
            || typeName.StartsWith("Lazy", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool IsUnderBuildOutput(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part =>
            part.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || part.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlySet<string> ReadPackageReferences(string projectFile)
    {
        if (!File.Exists(projectFile))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var document = XDocument.Load(projectFile);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    public static IReadOnlyList<string> ReadProjectReferences(string projectFile)
    {
        if (!File.Exists(projectFile))
        {
            return Array.Empty<string>();
        }

        var projectDirectory = Path.GetDirectoryName(projectFile) ?? Directory.GetCurrentDirectory();
        var document = XDocument.Load(projectFile);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizePath(Path.Combine(projectDirectory, value!)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool ProjectHasPackage(AnalyzedProject project, string packageFragment)
    {
        return project.PackageReferences.Any(package =>
            package.Contains(packageFragment, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsTestProjectName(string projectName)
    {
        return TestNameTokens.Any(token => HasToken(projectName, token));
    }

    public static bool HasTestFrameworkReference(IEnumerable<string> packageReferences)
    {
        return packageReferences.Any(package =>
            TestPackageMarkers.Any(marker => package.Contains(marker, StringComparison.OrdinalIgnoreCase)));
    }

    public static string InferLogicalLayer(string projectName, bool isWebProject, bool isTestProject)
    {
        if (isTestProject || IsTestProjectName(projectName))
        {
            return TestLayer;
        }

        if (isWebProject
            || HasToken(projectName, "Web")
            || HasToken(projectName, "Api")
            || HasToken(projectName, "PublicApi")
            || HasToken(projectName, "BlazorAdmin")
            || HasToken(projectName, "Presentation"))
        {
            return PresentationLayer;
        }

        if (HasToken(projectName, "ApplicationCore")
            || HasToken(projectName, "Domain")
            || HasToken(projectName, "Core"))
        {
            return DomainLayer;
        }

        if (HasToken(projectName, "Application"))
        {
            return ApplicationLayer;
        }

        if (HasToken(projectName, "Infrastructure")
            || HasToken(projectName, "Persistence")
            || HasToken(projectName, "Data"))
        {
            return InfrastructureLayer;
        }

        if (HasToken(projectName, "Shared")
            || HasToken(projectName, "Common")
            || HasToken(projectName, "Contracts"))
        {
            return SharedLayer;
        }

        return UnknownLayer;
    }

    public static bool IsProductionProject(AnalyzedProject project)
    {
        return !project.IsTestProject;
    }

    public static bool IsProductionEntryPointProject(AnalyzedProject project)
    {
        return !project.IsTestProject && project.IsAspNetCoreEntryPoint;
    }

    public static string BuildEvidence(params (string Label, string? Value)[] items)
    {
        var lines = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"- {item.Label}: {item.Value}")
            .ToArray();

        return lines.Length == 0
            ? "Evidence: DotDet found a matching analyzer signal."
            : $"Evidence:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    public static (string Key, IReadOnlyList<string> Cycle) NormalizeCycle(IEnumerable<string> cycle)
    {
        var nodes = cycle
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (nodes.Count > 1 && nodes[0].Equals(nodes[^1], StringComparison.OrdinalIgnoreCase))
        {
            nodes.RemoveAt(nodes.Count - 1);
        }

        if (nodes.Count == 0)
        {
            return (string.Empty, Array.Empty<string>());
        }

        var candidates = GetCycleRotations(nodes)
            .Concat(GetCycleRotations(nodes.AsEnumerable().Reverse().ToArray()))
            .Select(candidate => new
            {
                Nodes = candidate,
                Key = string.Join('|', candidate.Select(node => node.ToUpperInvariant()))
            })
            .OrderBy(candidate => candidate.Key, StringComparer.Ordinal)
            .ToArray();
        var canonical = candidates[0].Nodes;

        return (
            candidates[0].Key,
            canonical.Concat([canonical[0]]).ToArray());
    }

    public static bool HasToken(string value, string token)
    {
        return value.Split('.', '-', '_').Any(part => part.Equals(token, StringComparison.OrdinalIgnoreCase))
            || value.EndsWith(token, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<IReadOnlyList<string>> GetCycleRotations(IReadOnlyList<string> nodes)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            yield return nodes.Skip(index).Concat(nodes.Take(index)).ToArray();
        }
    }
}
