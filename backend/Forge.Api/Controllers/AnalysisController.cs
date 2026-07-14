using Forge.Api.Contracts;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Forge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AnalysisController : ControllerBase
{
    private readonly SolutionAnalysisService _solutionAnalysisService;
    private readonly ZipExtractionService _zipExtractionService;
    private readonly AnalysisHistoryStore _analysisHistoryStore;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        SolutionAnalysisService solutionAnalysisService,
        ZipExtractionService zipExtractionService,
        AnalysisHistoryStore analysisHistoryStore,
        IWebHostEnvironment environment,
        ILogger<AnalysisController> logger)
    {
        _solutionAnalysisService = solutionAnalysisService;
        _zipExtractionService = zipExtractionService;
        _analysisHistoryStore = analysisHistoryStore;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("analyze-path")]
    [ProducesResponseType<AnalysisResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResult>> AnalyzePath(
        [FromBody] AnalyzePathRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return BadRequest("A solution path is required.");
        }

        try
        {
            var result = await _solutionAnalysisService.AnalyzeAsync(
                request.SolutionPath,
                AnalysisInputTrust.TrustedLocalDevelopment,
                cancellationToken);
            var analysisRunId = await SaveHistoryIfAuthenticatedAsync(
                result,
                AnalysisSourceTypes.LocalDevPath,
                Path.GetFileName(request.SolutionPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                null,
                cancellationToken);
            return Ok(WithAnalysisRunId(result, analysisRunId));
        }
        catch (FileNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("analyze-sample")]
    [ProducesResponseType<AnalysisResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResult>> AnalyzeSample(CancellationToken cancellationToken)
    {
        try
        {
            var sampleSolutionPath = SolutionAnalysisService.ResolveSampleSolutionPath();
            var result = await _solutionAnalysisService.AnalyzeAsync(
                sampleSolutionPath,
                AnalysisInputTrust.TrustedLocalDevelopment,
                cancellationToken);
            var analysisRunId = await SaveHistoryIfAuthenticatedAsync(
                result,
                AnalysisSourceTypes.SampleProject,
                result.SolutionName,
                null,
                cancellationToken);
            return Ok(WithAnalysisRunId(result, analysisRunId));
        }
        catch (FileNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
    }

    [HttpPost("analyze-zip")]
    [RequestSizeLimit(250_000_000)]
    [ProducesResponseType<AnalysisResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResult>> AnalyzeZip(
        [FromForm] AnalyzeZipRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest("Upload a zip file in the 'file' form field.");
        }

        if (!Path.GetExtension(request.File.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only .zip uploads are supported.");
        }

        try
        {
            await using var extractedSolution = await _zipExtractionService.ExtractAsync(request.File, cancellationToken);
            var result = await _solutionAnalysisService.AnalyzeAsync(
                extractedSolution.SolutionPath,
                AnalysisInputTrust.UntrustedArchive,
                cancellationToken);
            var analysisRunId = await SaveHistoryIfAuthenticatedAsync(
                result,
                AnalysisSourceTypes.ZipUpload,
                request.File.FileName,
                null,
                cancellationToken);
            return Ok(WithAnalysisRunId(result, analysisRunId));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Zip analysis failed.");
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("history")]
    [ProducesResponseType<IReadOnlyList<AnalysisHistorySummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AnalysisHistorySummary>>> GetHistory(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var history = await _analysisHistoryStore.ListAsync(userId, cancellationToken);
        return Ok(history);
    }

    [HttpGet("history/{id}")]
    [ProducesResponseType<AnalysisHistoryDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisHistoryDetail>> GetHistoryItem(string id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var detail = await _analysisHistoryStore.GetAsync(userId, id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpDelete("history/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteHistoryItem(string id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var removed = await _analysisHistoryStore.DeleteAsync(userId, id, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpPost("history/{id}/rerun")]
    [ProducesResponseType<AnalysisResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResult>> RerunHistoryItem(string id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var detail = await _analysisHistoryStore.GetAsync(userId, id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        if (detail.Summary.SourceType != AnalysisSourceTypes.SampleProject)
        {
            return BadRequest("Re-run is currently supported for sample analyses only. Upload ZIP reports can be viewed and exported from history.");
        }

        try
        {
            var sampleSolutionPath = SolutionAnalysisService.ResolveSampleSolutionPath();
            var result = await _solutionAnalysisService.AnalyzeAsync(
                sampleSolutionPath,
                AnalysisInputTrust.TrustedLocalDevelopment,
                cancellationToken);
            await _analysisHistoryStore.SaveAsync(
                userId,
                result,
                AnalysisSourceTypes.SampleProject,
                result.SolutionName,
                cancellationToken: cancellationToken);

            return Ok(result);
        }
        catch (FileNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
    }

    private async Task<string?> SaveHistoryIfAuthenticatedAsync(
        AnalysisResult result,
        string sourceType,
        string sourceLabel,
        string? sourceUrl,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return null;
        }

        try
        {
            var run = await _analysisHistoryStore.SaveAsync(userId, result, sourceType, sourceLabel, sourceUrl, cancellationToken: cancellationToken);
            return run.Id;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Analysis completed but could not be saved to history.");
            return null;
        }
    }

    private static AnalysisResult WithAnalysisRunId(AnalysisResult result, string? analysisRunId)
    {
        return string.IsNullOrWhiteSpace(analysisRunId)
            ? result
            : result with { AnalysisRunId = analysisRunId };
    }

    private string? GetCurrentUserId()
    {
        return User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
    }
}
