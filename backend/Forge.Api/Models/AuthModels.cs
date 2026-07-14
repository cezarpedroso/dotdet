namespace Forge.Api.Models;

public sealed record DotDetUser(
    string GitHubUserId,
    string GitHubUsername,
    string? DisplayName,
    string? Email,
    string? AvatarUrl,
    DateTimeOffset CreatedDate,
    DateTimeOffset LastLoginDate);

public sealed record AuthUserResponse(
    string GitHubUserId,
    string GitHubUsername,
    string? DisplayName,
    string? Email,
    string? AvatarUrl,
    DateTimeOffset CreatedDate,
    DateTimeOffset LastLoginDate);

public sealed record AuthMeResponse(bool IsAuthenticated, AuthUserResponse? User);

public sealed record GitHubRepositoryAccessStatus(
    bool IsEnabled,
    DateTimeOffset? EnabledAt,
    DateTimeOffset? LastUpdatedAt);

public sealed record GitHubRepositoryAccessStatusResponse(
    bool IsEnabled,
    DateTimeOffset? EnabledAt,
    DateTimeOffset? LastUpdatedAt);
