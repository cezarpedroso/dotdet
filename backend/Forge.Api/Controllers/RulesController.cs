using Forge.Api.Models;
using Forge.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Forge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RulesController : ControllerBase
{
    private readonly RuleCatalogService _ruleCatalogService;

    public RulesController(RuleCatalogService ruleCatalogService)
    {
        _ruleCatalogService = ruleCatalogService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RuleDocumentation>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<RuleDocumentation>> GetRules()
    {
        return Ok(_ruleCatalogService.GetRules());
    }
}
