using Order.Application.Domain;

namespace Order.Application.Interfaces;

public interface IOrderRepository
{
    void Add(OrderEntity order);

    Task<List<OrderEntity>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<List<OrderEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<OrderEntity?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}
