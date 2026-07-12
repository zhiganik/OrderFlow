using Inventory.Application.Domain;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;

namespace Inventory.Application.Services;

public class StockService(
    IStockItemRepository stockItemRepository,
    IUnitOfWork unitOfWork) : IStockService
{
    public async Task<List<StockItemResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var stockItems = await stockItemRepository.GetAllAsync(cancellationToken);
        return stockItems.Select(ToResponse).ToList();
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
    };
}
