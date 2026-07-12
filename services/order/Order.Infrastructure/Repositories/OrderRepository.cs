using Microsoft.EntityFrameworkCore;
using Order.Application.Domain;
using Order.Application.Interfaces;
using Order.Infrastructure.Persistence;

namespace Order.Infrastructure.Repositories;

public class OrderRepository(OrderDbContext context) : IOrderRepository
{
    public void Add(OrderEntity order) => context.Orders.Add(order);

    public async Task<(List<OrderEntity> Items, int TotalCount)> GetPagedByCustomerIdAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Orders.AsNoTracking().Where(o => o.CustomerId == customerId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(List<OrderEntity> Items, int TotalCount)> GetAllPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await context.Orders.CountAsync(cancellationToken);

        var items = await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<OrderEntity?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public async Task<OrderEntity?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await context.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
}
