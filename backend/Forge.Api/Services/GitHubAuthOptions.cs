namespace Forge.Api.Services;

public sealed class GitHubAuthOptions
{
    public const string SectionName = "Authentication:GitHub";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string CallbackPath { get; set; } = "/signin-github";

    public string FrontendBaseUrl { get; set; } = string.Empty;

    public string UserStorePath { get; set; } = string.Empty;

    public string RepositoryAccessStorePath { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
