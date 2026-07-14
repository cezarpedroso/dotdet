using System.Security.Cryptography;
using System.Text.Json;
using Forge.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Forge.Api.Services;

public sealed class GitHubRepositoryAccessStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string storePath;
    private readonly IDataProtector protector;
    private readonly SemaphoreSlim gate = new(1, 1);

    public GitHubRepositoryAccessStore(
        IOptions<GitHubAuthOptions> options,
        IWebHostEnvironment environment,
        IDataProtectionProvider dataProtectionProvider)
        : this(
            string.IsNullOrWhiteSpace(options.Value.RepositoryAccessStorePath)
                ? Path.Combine(environment.ContentRootPath, "Data", "dotdet-github-repository-access.json")
                : options.Value.RepositoryAccessStorePath,
            dataProtectionProvider)
    {
    }

    public GitHubRepositoryAccessStore(string storePath, IDataProtectionProvider dataProtectionProvider)
    {
        this.storePath = storePath;
        protector = dataProtectionProvider.CreateProtector("DotDet.GitHub.RepositoryAccessToken.v1");
    }

    public async Task<GitHubRepositoryAccessStatus> GetStatusAsync(
        string githubUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(githubUserId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadUnsafeAsync(cancellationToken);
            var record = records.FirstOrDefault(item => item.GitHubUserId == githubUserId);
            return record is null
                ? new GitHubRepositoryAccessStatus(false, null, null)
                : new GitHubRepositoryAccessStatus(true, record.EnabledAt, record.LastUpdatedAt);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(
        string githubUserId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(githubUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadUnsafeAsync(cancellationToken);
            var existing = records.FirstOrDefault(item => item.GitHubUserId == githubUserId);
            var now = DateTimeOffset.UtcNow;
            var record = new StoredGitHubRepositoryAccess
            {
                GitHubUserId = githubUserId,
                ProtectedAccessToken = protector.Protect(accessToken),
                EnabledAt = existing?.EnabledAt ?? now,
                LastUpdatedAt = now
            };

            records.RemoveAll(item => item.GitHubUserId == githubUserId);
            records.Add(record);
            records.Sort((left, right) => string.Compare(left.GitHubUserId, right.GitHubUserId, StringComparison.Ordinal));
            await SaveUnsafeAsync(records, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string?> GetAccessTokenAsync(
        string githubUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(githubUserId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadUnsafeAsync(cancellationToken);
            var record = records.FirstOrDefault(item => item.GitHubUserId == githubUserId);
            if (record is null)
            {
                return null;
            }

            try
            {
                return protector.Unprotect(record.ProtectedAccessToken);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(
        string githubUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(githubUserId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadUnsafeAsync(cancellationToken);
            var removed = records.RemoveAll(item => item.GitHubUserId == githubUserId) > 0;
            if (removed)
            {
                await SaveUnsafeAsync(records, cancellationToken);
            }

            return removed;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<StoredGitHubRepositoryAccess>> LoadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(storePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(storePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<StoredGitHubRepositoryAccess>>(json, JsonOptions) ?? [];
    }

    private async Task SaveUnsafeAsync(
        List<StoredGitHubRepositoryAccess> records,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(storePath) ?? ".");
        await File.WriteAllTextAsync(storePath, JsonSerializer.Serialize(records, JsonOptions), cancellationToken);
    }

    private sealed record StoredGitHubRepositoryAccess
    {
        public required string GitHubUserId { get; init; }

        public required string ProtectedAccessToken { get; init; }

        public required DateTimeOffset EnabledAt { get; init; }

        public required DateTimeOffset LastUpdatedAt { get; init; }
    }
}
