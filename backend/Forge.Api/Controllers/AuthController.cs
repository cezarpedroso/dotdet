using System.Security.Claims;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Forge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthUserStore userStore;
    private readonly GitHubRepositoryAccessStore repositoryAccessStore;
    private readonly GitHubAuthOptions githubOptions;
    private readonly IWebHostEnvironment environment;

    public AuthController(
        AuthUserStore userStore,
        GitHubRepositoryAccessStore repositoryAccessStore,
        IOptions<GitHubAuthOptions> githubOptions,
        IWebHostEnvironment environment)
    {
        this.userStore = userStore;
        this.repositoryAccessStore = repositoryAccessStore;
        this.githubOptions = githubOptions.Value;
        this.environment = environment;
    }

    [HttpGet("github-login")]
    public IActionResult GitHubLogin()
    {
        if (!githubOptions.IsConfigured)
        {
            return Problem(
                title: "GitHub OAuth is not configured.",
                detail: "Set Authentication:GitHub:ClientId and Authentication:GitHub:ClientSecret before starting GitHub login.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var frontendBaseUrl = ResolveFrontendBaseUrl();
        if (frontendBaseUrl is null)
        {
            return FrontendConfigurationProblem();
        }

        var redirectUri = $"{frontendBaseUrl.TrimEnd('/')}/dashboard";
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, "GitHub");
    }

    [HttpGet("github-repo-access-login")]
    public IActionResult GitHubRepositoryAccessLogin()
    {
        if (!githubOptions.IsConfigured)
        {
            return Problem(
                title: "GitHub OAuth is not configured.",
                detail: "Set Authentication:GitHub:ClientId and Authentication:GitHub:ClientSecret before starting GitHub login.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var frontendBaseUrl = ResolveFrontendBaseUrl();
        if (frontendBaseUrl is null)
        {
            return FrontendConfigurationProblem();
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = $"{frontendBaseUrl.TrimEnd('/')}/analyze"
        };
        properties.Items["dotdet:repository_access"] = "true";
        properties.Items["dotdet:scope"] = "read:user user:email repo";

        return Challenge(properties, "GitHub");
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthMeResponse>> Me(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return new AuthMeResponse(false, null);
        }

        var githubUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(githubUserId))
        {
            return new AuthMeResponse(false, null);
        }

        var storedUser = await userStore.GetByGitHubIdAsync(githubUserId, cancellationToken);
        var responseUser = storedUser is not null
            ? ToResponse(storedUser)
            : new AuthUserResponse(
                githubUserId,
                User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("urn:github:login") ?? "github-user",
                User.FindFirstValue(ClaimTypes.GivenName),
                User.FindFirstValue(ClaimTypes.Email),
                User.FindFirstValue("urn:github:avatar_url"),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

        return new AuthMeResponse(true, responseUser);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("repository-access")]
    public async Task<ActionResult<GitHubRepositoryAccessStatusResponse>> GetRepositoryAccess(CancellationToken cancellationToken)
    {
        var githubUserId = GetCurrentUserId();
        if (githubUserId is null)
        {
            return Unauthorized();
        }

        var status = await repositoryAccessStore.GetStatusAsync(githubUserId, cancellationToken);
        return new GitHubRepositoryAccessStatusResponse(status.IsEnabled, status.EnabledAt, status.LastUpdatedAt);
    }

    [HttpDelete("repository-access")]
    public async Task<IActionResult> DisconnectRepositoryAccess(CancellationToken cancellationToken)
    {
        var githubUserId = GetCurrentUserId();
        if (githubUserId is null)
        {
            return Unauthorized();
        }

        await repositoryAccessStore.DeleteAsync(githubUserId, cancellationToken);
        return NoContent();
    }

    private static AuthUserResponse ToResponse(DotDetUser user)
    {
        return new AuthUserResponse(
            user.GitHubUserId,
            user.GitHubUsername,
            user.DisplayName,
            user.Email,
            user.AvatarUrl,
            user.CreatedDate,
            user.LastLoginDate);
    }

    private string? GetCurrentUserId()
    {
        return User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
    }

    private string? ResolveFrontendBaseUrl()
    {
        var configured = Uri.TryCreate(githubOptions.FrontendBaseUrl, UriKind.Absolute, out var configuredUri)
            && configuredUri.Scheme is "http" or "https"
            ? configuredUri.GetLeftPart(UriPartial.Authority)
            : null;

        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri)
            && IsAllowedFrontend(refererUri, configured))
        {
            return refererUri.GetLeftPart(UriPartial.Authority);
        }

        var origin = Request.Headers.Origin.ToString();
        if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            && IsAllowedFrontend(originUri, configured))
        {
            return originUri.GetLeftPart(UriPartial.Authority);
        }

        return configured;
    }

    private bool IsAllowedFrontend(Uri uri, string? configured)
    {
        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (configured is not null
            && string.Equals(uri.GetLeftPart(UriPartial.Authority), configured, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return environment.IsDevelopment()
            && uri.Port == 5173
            && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase));
    }

    private ObjectResult FrontendConfigurationProblem() => Problem(
        title: "Frontend URL is not configured.",
        detail: "Set Authentication:GitHub:FrontendBaseUrl to the public HTTPS frontend origin.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
