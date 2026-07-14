using System.Net;
using System.Text.Json;
using Forge.Api.Models;

namespace Forge.Api.Services;

public sealed class GitHubRepositoryService
{
    private const string GitHubApiBaseUrl = "https://api.github.com";
    private readonly HttpClient httpClient;

    public GitHubRepositoryService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public static GitHubRepositoryReference NormalizeRepositoryInput(string? repository, string? repositoryUrl = null)
    {
        var input = string.IsNullOrWhiteSpace(repositoryUrl) ? repository : repositoryUrl;
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Enter a public GitHub repository URL or owner/repo.");
        }

        var normalized = input.Trim();
        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Enter a valid GitHub repository URL.");
            }

            normalized = uri.AbsolutePath.Trim('/');
        }
        else
        {
            normalized = normalized
                .Replace("https://github.com/", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("http://github.com/", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("github.com/", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim('/');
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IsValidOwner(parts[0]) || !IsValidRepositoryName(parts[1]))
        {
            throw new ArgumentException("Enter a repository as owner/repo, for example dotnet/aspnetcore.");
        }

        return new GitHubRepositoryReference(parts[0], parts[1]);
    }

    public async Task<IReadOnlyList<GitHubRepositorySummary>> GetPublicRepositoriesForUserAsync(
        string githubUsername,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(githubUsername))
        {
            return [];
        }

        using var response = await SendGitHubRequestAsync(
            new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiBaseUrl}/users/{Uri.EscapeDataString(githubUsername)}/repos?type=owner&sort=updated&per_page=100"),
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return [];
        }

        ThrowIfRateLimited(response);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return payload.RootElement
            .EnumerateArray()
            .Where(repo => !GetJsonBoolean(repo, "private"))
            .Select(ToSummary)
            .Where(summary => !string.IsNullOrWhiteSpace(summary.Owner) && !string.IsNullOrWhiteSpace(summary.Name))
            .ToArray();
    }

    public async Task<IReadOnlyList<GitHubRepositorySummary>> GetRepositoriesForAuthenticatedUserAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var repositories = new List<GitHubRepositorySummary>();
        for (var page = 1; page <= 5; page++)
        {
            using var response = await SendGitHubRequestAsync(
                new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiBaseUrl}/user/repos?visibility=all&affiliation=owner,collaborator,organization_member&sort=updated&per_page=100&page={page}"),
                HttpCompletionOption.ResponseContentRead,
                cancellationToken,
                accessToken);

            ThrowIfRateLimited(response);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new GitHubRepositoryException("GitHub repository access needs to be reconnected.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new GitHubRepositoryException("GitHub repository listing is not available right now.");
            }

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var pageRepositories = payload.RootElement
                .EnumerateArray()
                .Select(ToSummary)
                .Where(summary => !string.IsNullOrWhiteSpace(summary.Owner) && !string.IsNullOrWhiteSpace(summary.Name))
                .ToArray();

            repositories.AddRange(pageRepositories);
            if (pageRepositories.Length < 100)
            {
                break;
            }
        }

        return repositories
            .DistinctBy(repository => $"{repository.Owner}/{repository.Name}", StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(IsLikelyDotNetRepository)
            .ThenByDescending(repository => repository.UpdatedAt ?? DateTimeOffset.MinValue)
            .ThenBy(repository => repository.Owner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<GitHubRepositoryMetadata> GetPublicRepositoryMetadataAsync(
        GitHubRepositoryReference repository,
        CancellationToken cancellationToken)
    {
        var metadata = await GetRepositoryMetadataAsync(repository, accessToken: null, allowPrivate: false, cancellationToken);
        if (metadata.IsPrivate)
        {
            throw new GitHubRepositoryException("Repository not found or not public.");
        }

        return metadata;
    }

    public async Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(
        GitHubRepositoryReference repository,
        string? accessToken,
        bool allowPrivate,
        CancellationToken cancellationToken)
    {
        using var response = await SendGitHubRequestAsync(
            new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiBaseUrl}/repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}"),
            HttpCompletionOption.ResponseContentRead,
            cancellationToken,
            accessToken);

        ThrowIfRateLimited(response);
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            throw new GitHubRepositoryException("Repository not found or access was not granted.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubRepositoryException("Repository not found or access was not granted.");
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = payload.RootElement;
        var isPrivate = GetJsonBoolean(root, "private");
        if (isPrivate && !allowPrivate)
        {
            throw new GitHubRepositoryException("Repository not found or not public.");
        }

        var owner = root.TryGetProperty("owner", out var ownerElement)
            ? GetJsonString(ownerElement, "login")
            : repository.Owner;
        var name = GetJsonString(root, "name") ?? repository.Name;
        var defaultBranch = GetJsonString(root, "default_branch");
        if (string.IsNullOrWhiteSpace(defaultBranch))
        {
            throw new GitHubRepositoryException("DotDet could not determine the repository's default branch.");
        }

        return new GitHubRepositoryMetadata(
            owner ?? repository.Owner,
            name,
            GetJsonString(root, "full_name") ?? $"{owner}/{name}",
            defaultBranch,
            GetJsonString(root, "html_url") ?? $"https://github.com/{owner}/{name}",
            GetJsonString(root, "description"),
            GetJsonDate(root, "updated_at"),
            isPrivate);
    }

    public async Task<DownloadedGitHubArchive> DownloadDefaultBranchArchiveAsync(
        GitHubRepositoryMetadata metadata,
        CancellationToken cancellationToken,
        string? accessToken = null)
    {
        var archiveUri = $"{GitHubApiBaseUrl}/repos/{Uri.EscapeDataString(metadata.Owner)}/{Uri.EscapeDataString(metadata.Name)}/zipball/{Uri.EscapeDataString(metadata.DefaultBranch)}";
        using var response = await SendGitHubRequestAsync(
            new HttpRequestMessage(HttpMethod.Get, archiveUri),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken,
            accessToken);

        ThrowIfRateLimited(response);
        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubRepositoryException("DotDet could not download the repository archive.");
        }

        if (response.Content.Headers.ContentLength > ZipExtractionService.MaxArchiveSizeBytes)
        {
            throw new GitHubRepositoryException("The repository archive is too large to analyze.");
        }

        var downloadRoot = Path.Combine(Path.GetTempPath(), "forge-github");
        Directory.CreateDirectory(downloadRoot);
        var archivePath = Path.Combine(downloadRoot, $"{Guid.NewGuid():N}.zip");

        try
        {
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = File.Create(archivePath);
            var buffer = new byte[81920];
            long totalBytes = 0;
            int bytesRead;

            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalBytes += bytesRead;
                if (totalBytes > ZipExtractionService.MaxArchiveSizeBytes)
                {
                    throw new GitHubRepositoryException("The repository archive is too large to analyze.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            return new DownloadedGitHubArchive(archivePath);
        }
        catch
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            throw;
        }
    }

    private async Task<HttpResponseMessage> SendGitHubRequestAsync(
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken,
        string? accessToken = null)
    {
        request.Headers.UserAgent.ParseAdd("DotDet/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new("Bearer", accessToken);
        }

        return await httpClient.SendAsync(request, completionOption, cancellationToken);
    }

    private static GitHubRepositorySummary ToSummary(JsonElement repo)
    {
        var owner = repo.TryGetProperty("owner", out var ownerElement)
            ? GetJsonString(ownerElement, "login")
            : null;

        return new GitHubRepositorySummary
        {
            Owner = owner ?? string.Empty,
            Name = GetJsonString(repo, "name") ?? string.Empty,
            Visibility = GetJsonBoolean(repo, "private") ? "Private" : "Public",
            DefaultBranch = GetJsonString(repo, "default_branch"),
            Description = GetJsonString(repo, "description"),
            HtmlUrl = GetJsonString(repo, "html_url"),
            UpdatedAt = GetJsonDate(repo, "updated_at")
        };
    }

    private static bool IsLikelyDotNetRepository(GitHubRepositorySummary repository)
    {
        var haystack = $"{repository.Name} {repository.Description}".ToLowerInvariant();
        return haystack.Contains(".net", StringComparison.Ordinal)
            || haystack.Contains("asp.net", StringComparison.Ordinal)
            || haystack.Contains("c#", StringComparison.Ordinal)
            || haystack.Contains("dotnet", StringComparison.Ordinal)
            || haystack.Contains("api", StringComparison.Ordinal);
    }

    private static void ThrowIfRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Forbidden)
        {
            return;
        }

        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
            && values.Any(value => value == "0"))
        {
            throw new GitHubRepositoryException("GitHub rate limit reached. Try again later.");
        }
    }

    private static bool IsValidOwner(string value)
    {
        return value.Length is > 0 and <= 39
            && value.All(character => char.IsLetterOrDigit(character) || character == '-')
            && !value.StartsWith("-", StringComparison.Ordinal)
            && !value.EndsWith("-", StringComparison.Ordinal);
    }

    private static bool IsValidRepositoryName(string value)
    {
        return value.Length is > 0 and <= 100
            && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? property.GetString()
            : null;
    }

    private static bool GetJsonBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static DateTimeOffset? GetJsonDate(JsonElement element, string propertyName)
    {
        var value = GetJsonString(element, propertyName);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }
}

public sealed record GitHubRepositoryReference(string Owner, string Name)
{
    public string FullName => $"{Owner}/{Name}";
}

public sealed record GitHubRepositoryMetadata(
    string Owner,
    string Name,
    string FullName,
    string DefaultBranch,
    string HtmlUrl,
    string? Description,
    DateTimeOffset? UpdatedAt,
    bool IsPrivate = false);

public sealed class DownloadedGitHubArchive : IAsyncDisposable
{
    public DownloadedGitHubArchive(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public FileStream OpenRead()
    {
        return File.OpenRead(FilePath);
    }

    public ValueTask DisposeAsync()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }

        return ValueTask.CompletedTask;
    }
}

public sealed class GitHubRepositoryException : Exception
{
    public GitHubRepositoryException(string message)
        : base(message)
    {
    }
}
