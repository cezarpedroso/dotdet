using Microsoft.AspNetCore.Mvc;
using SemanticFixture.Api.Services;

namespace SemanticFixture.Api.Controllers;

[ApiController]
[Route("api/widgets")]
public sealed class WidgetsController : ControllerBase
{
    private readonly IWidgetService _widgetService;

    public WidgetsController(IWidgetService widgetService)
    {
        _widgetService = widgetService;
    }

    [HttpGet("status")]
    public ActionResult<string> GetStatus()
    {
        return Ok(_widgetService.GetStatus());
    }
}
