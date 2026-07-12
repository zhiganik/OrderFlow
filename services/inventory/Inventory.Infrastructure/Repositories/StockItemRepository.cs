using Inventory.Application.Domain;
using Inventory.Application.Interfaces;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Repositories;

public class StockItemRepository(InventoryDbContext context) : IStockItemRepository
{
    public void Add(StockItem stockItem) => context.StockItems.Add(stockItem);

    public async Task<StockItem?> FindByProductNameAsync(string productName, CancellationToken cancellationToken = default) =>
        await context.StockItems.FirstOrDefaultAsync(s => s.ProductName == productName, cancellationToken);

    public async Task<List<StockItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.StockItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}
