using Inventory.Application.Domain;

namespace Inventory.Application.Interfaces;

public interface IStockItemRepository
{
    void Add(StockItem stockItem);

    Task<StockItem?> FindByProductNameAsync(string productName, CancellationToken cancellationToken = default);

    Task<List<StockItem>> GetAllAsync(CancellationToken cancellationToken = default);
}
