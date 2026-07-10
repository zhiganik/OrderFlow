using Order.Application.Dtos;

namespace Order.Application.Interfaces;

public interface IOrderService
{
    Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<List<OrderResponse>> GetOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<List<OrderResponse>> GetAllOrdersAsync(CancellationToken cancellationToken = default);

    Task<OrderResponse?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}
