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

    public async Task<(List<StockItem> Items, int TotalCount)> SearchAsync(
        int page,
        int pageSize,
        Guid? id,
        string? productName,
        CancellationToken cancellationToken = default)
    {
        var query = context.StockItems.AsNoTracking().AsQueryable();

        if (id is not null)
        {
            query = query.Where(s => s.Id == id);
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            query = query.Where(s => s.ProductName.Contains(productName));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
