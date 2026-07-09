using Forge.Api.Contracts;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Forge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AnalysisController : ControllerBase
{
    private readonly SolutionAnalysisService _solutionAnalysisService;
    private readonly ZipExtractionService _zipExtractionService;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        SolutionAnalysisService solutionAnalysisService,
        ZipExtractionService zipExtractionService,
        ILogger<AnalysisController> logger)
    {
        _solutionAnalysisService = solutionAnalysisService;
        _zipExtractionService = zipExtractionService;
        _logger = logger;
    }

    [HttpPost("analyze-path")]
    [ProducesResponseType<AnalysisResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResult>> AnalyzePath(
        [FromBody] AnalyzePathRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return BadRequest("A solution path is required.");
        }

        try
        {
            var result = await _solutionAnalysisService.AnalyzeAsync(request.SolutionPath, cancellationToken);
            return Ok(result);
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
            var result = await _solutionAnalysisService.AnalyzeAsync(sampleSolutionPath, cancellationToken);
            return Ok(result);
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
            var result = await _solutionAnalysisService.AnalyzeAsync(extractedSolution.SolutionPath, cancellationToken);
            return Ok(result);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Zip analysis failed.");
            return BadRequest(exception.Message);
        }
    }
}
