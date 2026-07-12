using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IStockService
{
    Task<List<StockItemResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<StockItemResponse> UpsertAsync(UpsertStockItemRequest request, CancellationToken cancellationToken = default);
}
