using Forge.Api.Contracts;
using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Forge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SuppressionsController : ControllerBase
{
    private readonly SuppressionService _suppressionService;

    public SuppressionsController(SuppressionService suppressionService)
    {
        _suppressionService = suppressionService;
    }

    [HttpPost]
    [ProducesResponseType<RepositorySuppression>(StatusCodes.Status200OK)]
    public ActionResult<RepositorySuppression> Create([FromBody] CreateSuppressionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return BadRequest("A solution path is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RuleId))
        {
            return BadRequest("A rule ID is required.");
        }

        var suppression = _suppressionService.Create(new CreateSuppressionInput(
            request.SolutionPath,
            request.RuleId,
            request.File,
            request.Project,
            request.Reason,
            request.Status,
            request.Expiration));

        return Ok(suppression);
    }

    [HttpDelete("{suppressionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Delete(string suppressionId, [FromQuery] string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return BadRequest("A solution path is required.");
        }

        return _suppressionService.Remove(solutionPath, suppressionId)
            ? NoContent()
            : NotFound();
    }
}
