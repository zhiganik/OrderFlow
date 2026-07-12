using Inventory.Application.Domain;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using OrderFlow.Shared.Common;

namespace Inventory.Application.Services;

public class StockService(
    IStockItemRepository stockItemRepository,
    IUnitOfWork unitOfWork) : IStockService
{
    public async Task<PagedResult<StockItemResponse>> SearchAsync(
        int page,
        int pageSize,
        Guid? id,
        string? productName,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await stockItemRepository.SearchAsync(page, pageSize, id, productName, cancellationToken);

        return new PagedResult<StockItemResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<StockItemResponse> UpsertAsync(UpsertStockItemRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await stockItemRepository.FindByProductNameAsync(request.ProductName, cancellationToken);

        if (existing is not null)
        {
            existing.SetQuantity(request.QuantityAvailable);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ToResponse(existing);
        }

        var stockItem = StockItem.Create(request.ProductName, request.QuantityAvailable);
        stockItemRepository.Add(stockItem);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(stockItem);
    }

    private static StockItemResponse ToResponse(StockItem stockItem) => new()
    {
        Id = stockItem.Id,
        ProductName = stockItem.ProductName,
        QuantityAvailable = stockItem.QuantityAvailable,
        CreatedAt = stockItem.CreatedAt,
    };
}
