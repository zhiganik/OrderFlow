using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Shared.Auth;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.Admin)]
public class StockController(IStockService stockService) : ControllerBase
{
    [HttpGet("api/stock")]
    public async Task<IActionResult> GetStock(CancellationToken cancellationToken)
    {
        var stockItems = await stockService.GetAllAsync(cancellationToken);
        return Ok(stockItems);
    }

    [HttpPost("api/stock")]
    public async Task<IActionResult> UpsertStock([FromBody] UpsertStockItemRequest request, CancellationToken cancellationToken)
    {
        var stockItem = await stockService.UpsertAsync(request, cancellationToken);
        return Ok(stockItem);
    }
}
