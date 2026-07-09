using Forge.SampleShop.Application.Orders;
using Microsoft.AspNetCore.Mvc;

namespace Forge.SampleShop.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderWorkflow _orderWorkflow;

    public OrdersController(IOrderWorkflow orderWorkflow)
    {
        _orderWorkflow = orderWorkflow;
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(CancellationToken cancellationToken)
    {
        var order = await _orderWorkflow.GetLatestOrderAsync(cancellationToken);
        return Ok(order);
    }
}
