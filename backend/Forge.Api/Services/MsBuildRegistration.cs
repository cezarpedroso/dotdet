using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace Forge.Api.Services;

internal static class MsBuildRegistration
{
    private static readonly object SyncRoot = new();

    [ModuleInitializer]
    public static void Initialize()
    {
        try
        {
            EnsureRegistered();
        }
        catch
        {
            // Best effort only. Analysis paths still have their own fallback handling.
        }
    }

    public static void EnsureRegistered()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (MSBuildLocator.IsRegistered)
            {
                return;
            }

            var visualStudioInstance = MSBuildLocator
                .QueryVisualStudioInstances()
                .OrderByDescending(instance => instance.Version)
                .FirstOrDefault();

            if (visualStudioInstance is not null)
            {
                MSBuildLocator.RegisterInstance(visualStudioInstance);
                return;
            }

            var sdkPath = FindDotNetSdkMsBuildPath();
            if (sdkPath is not null)
            {
                MSBuildLocator.RegisterMSBuildPath(sdkPath);
                return;
            }

            MSBuildLocator.RegisterDefaults();
        }
    }

    private static string? FindDotNetSdkMsBuildPath()
    {
        var candidateRoots = new List<string?>()
        {
            Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            @"C:\Program Files\dotnet",
            @"C:\Program Files (x86)\dotnet"
        };

        candidateRoots.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => File.Exists(Path.Combine(path, "dotnet.exe"))));

        return candidateRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.Combine(root!, "sdk"))
            .Where(Directory.Exists)
            .SelectMany(Directory.EnumerateDirectories)
            .Where(path => File.Exists(Path.Combine(path, "MSBuild.dll")))
            .OrderByDescending(path => ParseSdkVersion(Path.GetFileName(path)))
            .FirstOrDefault();
    }

    private static Version ParseSdkVersion(string version)
    {
        var normalizedVersion = version.Split('-')[0];
        return Version.TryParse(normalizedVersion, out var parsedVersion)
            ? parsedVersion
            : new Version(0, 0);
    }
}
