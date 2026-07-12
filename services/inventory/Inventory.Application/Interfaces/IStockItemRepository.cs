using Inventory.Application.Domain;

namespace Inventory.Application.Interfaces;

public interface IStockItemRepository
{
    void Add(StockItem stockItem);

    Task<StockItem?> FindByProductNameAsync(string productName, CancellationToken cancellationToken = default);

    Task<List<StockItem>> FindByProductNamesAsync(IEnumerable<string> productNames, CancellationToken cancellationToken = default);

    Task<(List<StockItem> Items, int TotalCount)> SearchAsync(
        int page,
        int pageSize,
        Guid? id,
        string? productName,
        CancellationToken cancellationToken = default);
}
