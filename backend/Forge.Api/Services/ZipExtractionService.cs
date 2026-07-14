using System.IO.Compression;
using Forge.Api.Analysis;
using Microsoft.AspNetCore.Http;

namespace Forge.Api.Services;

public sealed class ZipExtractionService
{
    public const long MaxArchiveSizeBytes = 250_000_000;
    public const long MaxEntrySizeBytes = 100_000_000;
    public const long MaxTotalUncompressedSizeBytes = 750_000_000;
    public const int MaxEntryCount = 25_000;

    public async Task<ExtractedSolution> ExtractAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new ArgumentException("The uploaded archive is empty.", nameof(file));
        }

        if (file.Length > MaxArchiveSizeBytes)
        {
            throw new InvalidDataException("The archive is too large to analyze.");
        }

        await using var uploadStream = file.OpenReadStream();
        return await ExtractAsync(uploadStream, file.FileName, cancellationToken);
    }

    public async Task<ExtractedSolution> ExtractAsync(Stream archiveStream, string archiveName, CancellationToken cancellationToken)
    {
        _ = archiveName;
        var extractionRoot = AnalyzerUtilities.NormalizePath(Path.Combine(Path.GetTempPath(), "forge", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(extractionRoot);

        try
        {
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
            var entryCount = 0;
            long totalUncompressedBytes = 0;
            var extractionRootWithSeparator = extractionRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                entryCount++;
                if (entryCount > MaxEntryCount)
                {
                    throw new InvalidDataException("The archive contains too many files to analyze.");
                }

                var normalizedEntryName = entry.FullName.Replace('\\', '/');
                var entryNameForValidation = normalizedEntryName.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(entryNameForValidation)
                    || Path.IsPathRooted(normalizedEntryName)
                    || normalizedEntryName.Contains(':')
                    || entryNameForValidation.Split('/').Any(segment => segment is ".." or ""))
                {
                    throw new InvalidOperationException("The archive contains an unsupported or unsafe path.");
                }

                if (entry.Length > MaxEntrySizeBytes)
                {
                    throw new InvalidDataException("The archive contains an individual file that is too large to analyze.");
                }

                totalUncompressedBytes += entry.Length;
                if (totalUncompressedBytes > MaxTotalUncompressedSizeBytes)
                {
                    throw new InvalidDataException("The archive expands to too much data to analyze.");
                }

                var destinationPath = AnalyzerUtilities.NormalizePath(Path.Combine(extractionRoot, normalizedEntryName));
                if (!destinationPath.StartsWith(extractionRootWithSeparator, StringComparison.OrdinalIgnoreCase)
                    && !destinationPath.Equals(extractionRoot, StringComparison.OrdinalIgnoreCase))
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
        catch
        {
            if (Directory.Exists(extractionRoot))
            {
                Directory.Delete(extractionRoot, recursive: true);
            }

            throw;
        }
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
