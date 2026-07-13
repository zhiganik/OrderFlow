using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.Api.Filters;
using Order.Application.Dtos;
using Order.Application.Interfaces;
using OrderFlow.Shared.Auth;

namespace Order.Api.Controllers;

[ApiController]
[Authorize]
public class OrdersController(IOrderService orderService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orders = currentUser.IsAdmin
            ? await orderService.GetAllOrdersAsync(page, pageSize, cancellationToken)
            : await orderService.GetOrdersByCustomerAsync(currentUser.UserId, page, pageSize, cancellationToken);

        return Ok(orders);
    }
    
    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderByIdAsync(id, cancellationToken);

        if (order is null || (!currentUser.IsAdmin && order.CustomerId != currentUser.UserId))
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpPost("orders")]
    [RequireIdempotencyKey]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var idempotencyKey = (string)HttpContext.Items[RequireIdempotencyKeyAttribute.ContextItemKey]!;

        var result = await orderService.CreateOrderAsync(request, currentUser.UserId, idempotencyKey, cancellationToken);

        return result.Outcome switch
        {
            CreateOrderOutcome.Conflict => Problem(
                title: "Idempotency-Key was reused with a different request body.",
                statusCode: StatusCodes.Status409Conflict),
            _ => StatusCode(result.StatusCode, result.Order),
        };
    }

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderByIdAsync(id, cancellationToken);

        if (order is null || (!currentUser.IsAdmin && order.CustomerId != currentUser.UserId))
        {
            return NotFound();
        }

        var result = await orderService.CancelOrderAsync(id, cancellationToken);

        return result.Outcome switch
        {
            CancelOrderOutcome.Canceled => Ok(result.Order),
            CancelOrderOutcome.InvalidStatus => Problem(
                title: $"Order cannot be canceled while in '{order.Status}' status.",
                statusCode: StatusCodes.Status409Conflict),
            _ => NotFound(),
        };
    }
}
