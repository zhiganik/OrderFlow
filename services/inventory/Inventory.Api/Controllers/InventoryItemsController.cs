using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
public class InventoryItemsController : ControllerBase
{
    [HttpGet("inventory-items")]
    public IActionResult GetInventoryItems()
    {
        var userId = Request.Headers["X-User-Id"].ToString();
        return Ok(new { userId, items = Array.Empty<object>() });
    }
}
