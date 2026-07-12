using Inventory.Application.Dtos;
using OrderFlow.Shared.Common;

namespace Inventory.Application.Interfaces;

public interface IStockService
{
    Task<PagedResult<StockItemResponse>> SearchAsync(
        int page,
        int pageSize,
        Guid? id,
        string? productName,
        CancellationToken cancellationToken = default);

    Task<StockItemResponse> UpsertAsync(UpsertStockItemRequest request, CancellationToken cancellationToken = default);
}
