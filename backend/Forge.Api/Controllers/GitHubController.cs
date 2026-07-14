using System.Security.Claims;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Forge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class GitHubController : ControllerBase
{
    private readonly GitHubRepositoryService githubRepositoryService;
    private readonly ZipExtractionService zipExtractionService;
    private readonly SolutionAnalysisService solutionAnalysisService;
    private readonly AnalysisHistoryStore analysisHistoryStore;
    private readonly GitHubRepositoryAccessStore repositoryAccessStore;
    private readonly ILogger<GitHubController> logger;

    public GitHubController(
        GitHubRepositoryService githubRepositoryService,
        ZipExtractionService zipExtractionService,
        SolutionAnalysisService solutionAnalysisService,
        AnalysisHistoryStore analysisHistoryStore,
        GitHubRepositoryAccessStore repositoryAccessStore,
        ILogger<GitHubController> logger)
    {
        this.githubRepositoryService = githubRepositoryService;
        this.zipExtractionService = zipExtractionService;
        this.solutionAnalysisService = solutionAnalysisService;
        this.analysisHistoryStore = analysisHistoryStore;
        this.repositoryAccessStore = repositoryAccessStore;
        this.logger = logger;
    }

    [HttpGet("repos")]
    [ProducesResponseType<GitHubRepositoryListingResponse>(StatusCodes.Status200OK)]
    [HttpGet("public-repos")]
    [ProducesResponseType<GitHubRepositoryListingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GitHubRepositoryListingResponse>> GetRepositories(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var username = User.FindFirstValue("urn:github:login") ?? User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(username))
        {
            return Ok(CreateUnavailableListing("Repository listing is not available with the current GitHub connection."));
        }

        try
        {
            var privateAccessToken = await repositoryAccessStore.GetAccessTokenAsync(userId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(privateAccessToken))
            {
                var authenticatedRepositories = await githubRepositoryService.GetRepositoriesForAuthenticatedUserAsync(privateAccessToken, cancellationToken);
                return Ok(new GitHubRepositoryListingResponse
                {
                    Enabled = true,
                    IsAvailable = true,
                    PrivateAccessEnabled = true,
                    PrivateAccessMessage = "Private repository access is enabled for this GitHub account.",
                    Message = authenticatedRepositories.Count == 0
                        ? "No repositories were found for this GitHub account. Paste a repository URL to analyze it."
                        : "Repositories loaded. Public and private repositories are available.",
                    Repositories = authenticatedRepositories
                });
            }

            var publicRepositories = await githubRepositoryService.GetPublicRepositoriesForUserAsync(username, cancellationToken);
            return Ok(new GitHubRepositoryListingResponse
            {
                Enabled = true,
                IsAvailable = true,
                PrivateAccessEnabled = false,
                PrivateAccessMessage = "Enable private repository access to list and analyze private repositories.",
                Message = publicRepositories.Count == 0
                    ? "No public repositories were found for this GitHub account. Paste a public repository URL to analyze it or enable private access."
                    : "Public repositories loaded. Enable private repository access to include private repositories.",
                Repositories = publicRepositories
            });
        }
        catch (GitHubRepositoryException exception)
        {
            return Ok(CreateUnavailableListing(exception.Message));
        }
        catch (Exception exception)
        {
            logger.LogWarning("Public GitHub repository listing failed: {ExceptionType}", exception.GetType().Name);
            return Ok(CreateUnavailableListing("Repository listing is not available with the current GitHub connection."));
        }
    }

    [HttpPost("analyze-repo")]
    [EnableRateLimiting("analysis")]
    [AnalysisExecution]
    [ProducesResponseType<AnalysisResult>(StatusCodes.Status200OK)]
    public Task<ActionResult<AnalysisResult>> AnalyzeRepository(
        [FromBody] AnalyzePublicRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        return AnalyzeRepositoryCore(request, cancellationToken);
    }

    [HttpPost("analyze-public-repo")]
    [EnableRateLimiting("analysis")]
    [AnalysisExecution]
    [ProducesResponseType<AnalysisResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResult>> AnalyzePublicRepository(
        [FromBody] AnalyzePublicRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        return await AnalyzeRepositoryCore(request, cancellationToken);
    }

    private async Task<ActionResult<AnalysisResult>> AnalyzeRepositoryCore(
        AnalyzePublicRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        GitHubRepositoryReference repository;
        try
        {
            repository = GitHubRepositoryService.NormalizeRepositoryInput(request.Repository, request.RepositoryUrl);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        GitHubRepositoryMetadata? metadata = null;
        try
        {
            var repositoryAccessToken = await repositoryAccessStore.GetAccessTokenAsync(userId, cancellationToken);
            metadata = await githubRepositoryService.GetRepositoryMetadataAsync(
                repository,
                repositoryAccessToken,
                allowPrivate: !string.IsNullOrWhiteSpace(repositoryAccessToken),
                cancellationToken);

            if (metadata.IsPrivate && string.IsNullOrWhiteSpace(repositoryAccessToken))
            {
                return BadRequest("Private repository access is not enabled for this GitHub account.");
            }

            await using var downloadedArchive = await githubRepositoryService.DownloadDefaultBranchArchiveAsync(
                metadata,
                cancellationToken,
                repositoryAccessToken);
            await using var archiveStream = downloadedArchive.OpenRead();
            await using var extractedSolution = await zipExtractionService.ExtractAsync(
                archiveStream,
                $"{metadata.FullName}-{metadata.DefaultBranch}.zip",
                cancellationToken);

            var result = await solutionAnalysisService.AnalyzeAsync(
                extractedSolution.SolutionPath,
                AnalysisInputTrust.UntrustedArchive,
                cancellationToken);
            var run = await analysisHistoryStore.SaveAsync(
                userId,
                result,
                AnalysisSourceTypes.GitHubRepo,
                metadata.FullName,
                metadata.HtmlUrl,
                metadata.Owner,
                metadata.Name,
                metadata.DefaultBranch,
                metadata.IsPrivate ? "Private" : "Public",
                cancellationToken);
            result = result with { AnalysisRunId = run.Id };

            return Ok(AnalysisResultSanitizer.CreateLiveResponse(result));
        }
        catch (GitHubRepositoryException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (FileNotFoundException)
        {
            return BadRequest("No .sln or .slnx file was found in this repository.");
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException)
        {
            return BadRequest("Repository analysis failed during extraction. The archive may contain unsupported paths.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "GitHub repository analysis failed for {Repository}: {ExceptionType}",
                metadata?.FullName ?? repository.FullName,
                exception.GetType().Name);
            return BadRequest("Repository analysis failed. Try again later.");
        }
    }

    private static GitHubRepositoryListingResponse CreateUnavailableListing(string reason)
    {
        return new GitHubRepositoryListingResponse
        {
            Enabled = false,
            IsAvailable = false,
            Reason = reason,
            PrivateAccessEnabled = false,
            PrivateAccessMessage = "Enable private repository access to list and analyze private repositories.",
            Message = $"{reason} Paste a GitHub repository URL to analyze it.",
            Repositories = []
        };
    }

    private string? GetCurrentUserId()
    {
        return User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
    }
}
