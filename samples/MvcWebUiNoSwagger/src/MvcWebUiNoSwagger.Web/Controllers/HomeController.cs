using Microsoft.AspNetCore.Mvc;
using MvcWebUiNoSwagger.Services.Support;

namespace MvcWebUiNoSwagger.Web.Controllers;

public sealed class HomeController(
    ISupportDashboard dashboard,
    ILogger<HomeController> logger,
    IConfiguration configuration) : Controller
{
    public IActionResult Index()
    {
        logger.LogInformation("Rendering support dashboard for {Environment}", configuration["EnvironmentLabel"]);
        return View(dashboard.GetRecentCases());
    }

    public IActionResult Error() => View();
}
