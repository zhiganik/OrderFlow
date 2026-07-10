using Order.Application.Interfaces;
using Order.Infrastructure.Persistence;

namespace Order.Infrastructure.Repositories;

public class UnitOfWork(OrderDbContext context) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
