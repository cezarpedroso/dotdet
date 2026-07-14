using System.Text.Json;
using Forge.Api.Models;
using Microsoft.Extensions.Options;

namespace Forge.Api.Services;

public sealed class AuthUserStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string storePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public AuthUserStore(IOptions<GitHubAuthOptions> options, IWebHostEnvironment environment)
    {
        storePath = string.IsNullOrWhiteSpace(options.Value.UserStorePath)
            ? Path.Combine(environment.ContentRootPath, "Data", "dotdet-users.json")
            : options.Value.UserStorePath;
    }

    public async Task<DotDetUser?> GetByGitHubIdAsync(string githubUserId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUnsafeAsync(cancellationToken);
            return users.FirstOrDefault(user => user.GitHubUserId == githubUserId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<DotDetUser> UpsertFromGitHubAsync(
        string githubUserId,
        string githubUsername,
        string? displayName,
        string? email,
        string? avatarUrl,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUnsafeAsync(cancellationToken);
            var existing = users.FirstOrDefault(user => user.GitHubUserId == githubUserId);
            var now = DateTimeOffset.UtcNow;
            var user = existing is null
                ? new DotDetUser(githubUserId, githubUsername, displayName, email, avatarUrl, now, now)
                : existing with
                {
                    GitHubUsername = githubUsername,
                    DisplayName = displayName,
                    Email = email,
                    AvatarUrl = avatarUrl,
                    LastLoginDate = now
                };

            users.RemoveAll(candidate => candidate.GitHubUserId == githubUserId);
            users.Add(user);
            users.Sort((left, right) => string.Compare(left.GitHubUsername, right.GitHubUsername, StringComparison.OrdinalIgnoreCase));

            Directory.CreateDirectory(Path.GetDirectoryName(storePath) ?? ".");
            await File.WriteAllTextAsync(storePath, JsonSerializer.Serialize(users, JsonOptions), cancellationToken);

            return user;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<DotDetUser>> LoadUnsafeAsync(CancellationToken cancellationToken)
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

        return JsonSerializer.Deserialize<List<DotDetUser>>(json, JsonOptions) ?? [];
    }
}
