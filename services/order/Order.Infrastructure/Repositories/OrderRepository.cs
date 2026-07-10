using Microsoft.EntityFrameworkCore;
using Order.Application.Domain;
using Order.Application.Interfaces;
using Order.Infrastructure.Persistence;

namespace Order.Infrastructure.Repositories;

public class OrderRepository(OrderDbContext context) : IOrderRepository
{
    public void Add(OrderEntity order) => context.Orders.Add(order);

    public async Task<List<OrderEntity>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken);

    public async Task<List<OrderEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ToListAsync(cancellationToken);

    public async Task<OrderEntity?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
}
