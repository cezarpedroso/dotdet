using System.Security.Claims;
using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using Forge.Api.Analysis;
using Forge.Api.Analyzers;
using Forge.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var hostingOptions = builder.Configuration.GetSection(DotDetHostingOptions.SectionName).Get<DotDetHostingOptions>() ?? new();
var analysisOptions = builder.Configuration.GetSection(AnalysisExecutionOptions.SectionName).Get<AnalysisExecutionOptions>() ?? new();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DotDetDashboard", policy =>
    {
        if (hostingOptions.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(hostingOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

builder.Services.Configure<GitHubAuthOptions>(builder.Configuration.GetSection(GitHubAuthOptions.SectionName));
builder.Services.Configure<DotDetHostingOptions>(builder.Configuration.GetSection(DotDetHostingOptions.SectionName));
builder.Services.Configure<AnalysisExecutionOptions>(builder.Configuration.GetSection(AnalysisExecutionOptions.SectionName));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = 2;
    foreach (var value in hostingOptions.KnownProxies)
    {
        if (IPAddress.TryParse(value, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("analysis", context => RateLimitPartition.GetFixedWindowLimiter(
        AnalysisExecutionMiddleware.GetCallerKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, analysisOptions.PermitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, analysisOptions.WindowSeconds)),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Analysis request limit reached",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Too many analysis requests were submitted. Wait briefly and try again."
        }, cancellationToken);
    };
});
builder.Services.AddDataProtection();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();

builder.Services.AddScoped<SolutionAnalysisService>();
builder.Services.AddSingleton<SemanticAnalysisHelper>();
builder.Services.AddSingleton<IssueEnrichmentService>();
builder.Services.AddSingleton<RuleCatalogService>();
builder.Services.AddSingleton<FindingGroupingService>();
builder.Services.AddSingleton<ArchitectureMapService>();
builder.Services.AddSingleton<EngineeringAssessmentService>();
builder.Services.AddSingleton<SuppressionService>();
builder.Services.AddSingleton<AuthUserStore>();
builder.Services.AddSingleton<GitHubRepositoryAccessStore>();
builder.Services.AddSingleton<AnalysisHistoryStore>();
builder.Services.AddScoped<ArchitectureAnalyzer>();
builder.Services.AddScoped<DependencyInjectionAnalyzer>();
builder.Services.AddScoped<EfCoreAnalyzer>();
builder.Services.AddScoped<SecurityConfigurationAnalyzer>();
builder.Services.AddScoped<ApiReadinessAnalyzer>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<ZipExtractionService>();
builder.Services.AddSingleton<AnalysisConcurrencyGate>();
builder.Services.AddHttpClient<GitHubRepositoryService>();

var githubAuthOptions = builder.Configuration.GetSection(GitHubAuthOptions.SectionName).Get<GitHubAuthOptions>() ?? new GitHubAuthOptions();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "GitHub";
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = ".DotDet.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddOAuth("GitHub", options =>
    {
        options.ClientId = githubAuthOptions.IsConfigured ? githubAuthOptions.ClientId : "dotdet-github-oauth-not-configured";
        options.ClientSecret = githubAuthOptions.IsConfigured ? githubAuthOptions.ClientSecret : "dotdet-github-oauth-not-configured";
        options.CallbackPath = githubAuthOptions.CallbackPath;
        options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        options.TokenEndpoint = "https://github.com/login/oauth/access_token";
        options.UserInformationEndpoint = "https://api.github.com/user";
        options.Scope.Add("read:user");
        options.Scope.Add("user:email");
        options.SaveTokens = false;
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.SameSite = builder.Environment.IsDevelopment()
            ? SameSiteMode.Lax
            : SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            var redirectUri = context.RedirectUri;
            if (context.Properties.Items.TryGetValue("dotdet:scope", out var scopeOverride)
                && !string.IsNullOrWhiteSpace(scopeOverride))
            {
                redirectUri = ReplaceQueryParameter(redirectUri, "scope", scopeOverride);
            }

            context.Response.Redirect(redirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnCreatingTicket = async context =>
        {
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            userRequest.Headers.Accept.ParseAdd("application/json");
            userRequest.Headers.UserAgent.ParseAdd("DotDet/1.0");
            userRequest.Headers.Authorization = new("Bearer", context.AccessToken);

            using var userResponse = await context.Backchannel.SendAsync(userRequest, context.HttpContext.RequestAborted);
            userResponse.EnsureSuccessStatusCode();
            using var userPayload = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));

            var root = userPayload.RootElement;
            var githubUserId = GetJsonString(root, "id");
            var githubUsername = GetJsonString(root, "login");
            if (string.IsNullOrWhiteSpace(githubUserId) || string.IsNullOrWhiteSpace(githubUsername))
            {
                throw new InvalidOperationException("GitHub OAuth response did not include a user id and login.");
            }

            var displayName = GetJsonString(root, "name");
            var email = GetJsonString(root, "email");
            var avatarUrl = GetJsonString(root, "avatar_url");
            email ??= await GetPrimaryGitHubEmailAsync(context, context.AccessToken);

            var store = context.HttpContext.RequestServices.GetRequiredService<AuthUserStore>();
            var storedUser = await store.UpsertFromGitHubAsync(
                githubUserId,
                githubUsername,
                displayName,
                email,
                avatarUrl,
                context.HttpContext.RequestAborted);

            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, storedUser.GitHubUserId));
            identity.AddClaim(new Claim(ClaimTypes.Name, storedUser.GitHubUsername));
            identity.AddClaim(new Claim("urn:github:login", storedUser.GitHubUsername));
            if (!string.IsNullOrWhiteSpace(storedUser.DisplayName))
            {
                identity.AddClaim(new Claim(ClaimTypes.GivenName, storedUser.DisplayName));
            }

            if (!string.IsNullOrWhiteSpace(storedUser.Email))
            {
                identity.AddClaim(new Claim(ClaimTypes.Email, storedUser.Email));
            }

            if (!string.IsNullOrWhiteSpace(storedUser.AvatarUrl))
            {
                identity.AddClaim(new Claim("urn:github:avatar_url", storedUser.AvatarUrl));
            }

            if (context.Properties.Items.TryGetValue("dotdet:repository_access", out var repositoryAccess)
                && repositoryAccess == "true"
                && !string.IsNullOrWhiteSpace(context.AccessToken))
            {
                var repositoryAccessStore = context.HttpContext.RequestServices.GetRequiredService<GitHubRepositoryAccessStore>();
                await repositoryAccessStore.SaveAsync(
                    storedUser.GitHubUserId,
                    context.AccessToken,
                    context.HttpContext.RequestAborted);
            }
        };
    });

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("DotDetDashboard");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<AnalysisExecutionMiddleware>();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

static string? GetJsonString(JsonElement root, string propertyName)
{
    if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
    {
        return null;
    }

    return property.ValueKind == JsonValueKind.Number ? property.GetRawText() : property.GetString();
}

static async Task<string?> GetPrimaryGitHubEmailAsync(Microsoft.AspNetCore.Authentication.OAuth.OAuthCreatingTicketContext context, string? accessToken)
{
    if (string.IsNullOrWhiteSpace(accessToken))
    {
        return null;
    }

    using var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
    emailRequest.Headers.Accept.ParseAdd("application/json");
    emailRequest.Headers.UserAgent.ParseAdd("DotDet/1.0");
    emailRequest.Headers.Authorization = new("Bearer", accessToken);

    using var emailResponse = await context.Backchannel.SendAsync(emailRequest, context.HttpContext.RequestAborted);
    if (!emailResponse.IsSuccessStatusCode)
    {
        return null;
    }

    using var emailPayload = JsonDocument.Parse(await emailResponse.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
    foreach (var email in emailPayload.RootElement.EnumerateArray())
    {
        var primary = email.TryGetProperty("primary", out var primaryProperty) && primaryProperty.GetBoolean();
        var verified = email.TryGetProperty("verified", out var verifiedProperty) && verifiedProperty.GetBoolean();
        if (primary && verified)
        {
            return GetJsonString(email, "email");
        }
    }

    return null;
}

static string ReplaceQueryParameter(string uri, string key, string value)
{
    var builder = new UriBuilder(uri);
    var query = builder.Query.TrimStart('?');
    var parts = string.IsNullOrWhiteSpace(query)
        ? new List<string>()
        : query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.StartsWith($"{Uri.EscapeDataString(key)}=", StringComparison.OrdinalIgnoreCase)
                && !part.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
            .ToList();

    parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
    builder.Query = string.Join('&', parts);
    return builder.Uri.ToString();
}
