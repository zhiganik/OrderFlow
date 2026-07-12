using Order.Application.Domain;

namespace Order.Application.Interfaces;

public interface IOrderRepository
{
    void Add(OrderEntity order);

    Task<(List<OrderEntity> Items, int TotalCount)> GetPagedByCustomerIdAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(List<OrderEntity> Items, int TotalCount)> GetAllPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<OrderEntity?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<OrderEntity?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}
