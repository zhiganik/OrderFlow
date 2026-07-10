using Microsoft.AspNetCore.Mvc;

namespace Order.Api.Controllers;

[ApiController]
public class OrdersController : ControllerBase
{
    [HttpGet("orders")]
    public IActionResult GetOrders()
    {
        var userId = Request.Headers["X-User-Id"].ToString();
        return Ok(new { userId, orders = Array.Empty<object>() });
    }
}
