using Order.Application.Dtos;
using OrderFlow.Shared.Common;

namespace Order.Application.Interfaces;

public interface IOrderService
{
    Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OrderResponse>> GetOrdersByCustomerAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OrderResponse>> GetAllOrdersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<OrderResponse?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task MarkReservedAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task MarkRejectedAsync(Guid orderId, string reason, CancellationToken cancellationToken = default);
}
