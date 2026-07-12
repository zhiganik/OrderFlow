using Inventory.Application.Interfaces;
using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Repositories;

public class UnitOfWork(InventoryDbContext context) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
