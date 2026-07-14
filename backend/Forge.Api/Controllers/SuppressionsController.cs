using Forge.Api.Contracts;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Forge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SuppressionsController : ControllerBase
{
    private const string SuppressionsUnavailableMessage = "Suppressions are not available for this analysis source yet.";

    private readonly AnalysisHistoryStore _analysisHistoryStore;

    public SuppressionsController(AnalysisHistoryStore analysisHistoryStore)
    {
        _analysisHistoryStore = analysisHistoryStore;
    }

    [HttpPost]
    [ProducesResponseType<RepositorySuppression>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RepositorySuppression>> Create(
        [FromBody] CreateSuppressionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.AnalysisRunId))
        {
            return BadRequest(SuppressionsUnavailableMessage);
        }

        if (string.IsNullOrWhiteSpace(request.FindingId))
        {
            return BadRequest("A finding ID is required.");
        }

        var suppression = await _analysisHistoryStore.CreateSuppressionAsync(
            userId,
            request.AnalysisRunId,
            request.FindingId,
            request.Reason,
            request.Status,
            request.Expiration,
            cancellationToken);

        if (suppression is null)
        {
            return NotFound();
        }

        return Ok(suppression);
    }

    [HttpDelete("{suppressionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        string suppressionId,
        [FromQuery] string? analysisRunId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(analysisRunId))
        {
            return BadRequest(SuppressionsUnavailableMessage);
        }

        return await _analysisHistoryStore.RemoveSuppressionAsync(
            userId,
            analysisRunId,
            suppressionId,
            cancellationToken)
            ? NoContent()
            : NotFound();
    }

    private string? GetCurrentUserId()
    {
        return User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
    }
}
