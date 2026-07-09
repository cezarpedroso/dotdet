using System.IO.Compression;
using Forge.Api.Analysis;
using Microsoft.AspNetCore.Http;

namespace Forge.Api.Services;

public sealed class ZipExtractionService
{
    public async Task<ExtractedSolution> ExtractAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new ArgumentException("The uploaded archive is empty.", nameof(file));
        }

        var extractionRoot = Path.Combine(Path.GetTempPath(), "forge", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractionRoot);

        await using var uploadStream = file.OpenReadStream();
        using var archive = new ZipArchive(uploadStream, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var entry in archive.Entries)
        {
            var destinationPath = AnalyzerUtilities.NormalizePath(Path.Combine(extractionRoot, entry.FullName));
            if (!destinationPath.StartsWith(extractionRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The archive contains an entry outside the extraction directory.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using var entryStream = entry.Open();
            await using var destinationStream = File.Create(destinationPath);
            await entryStream.CopyToAsync(destinationStream, cancellationToken);
        }

        var solutionPath = SolutionAnalysisService.ResolveSolutionPath(extractionRoot);
        return new ExtractedSolution(extractionRoot, solutionPath);
    }
}

public sealed class ExtractedSolution : IAsyncDisposable
{
    public ExtractedSolution(string rootPath, string solutionPath)
    {
        RootPath = rootPath;
        SolutionPath = solutionPath;
    }

    public string RootPath { get; }

    public string SolutionPath { get; }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
