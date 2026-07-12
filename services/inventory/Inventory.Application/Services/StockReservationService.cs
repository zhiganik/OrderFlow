using Inventory.Application.Interfaces;
using MassTransit;
using OrderFlow.Contracts;

namespace Inventory.Application.Services;

public class StockReservationService(
    IStockItemRepository stockItemRepository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint) : IStockReservationService
{
    public async Task ReserveOrRejectAsync(OrderCreatedEvent orderCreated, CancellationToken cancellationToken = default)
    {
        var productNames = orderCreated.Items.Select(i => i.ProductName).Distinct().ToList();
        var stockItems = await stockItemRepository.FindByProductNamesAsync(productNames, cancellationToken);
        var byProductName = stockItems.ToDictionary(s => s.ProductName);

        string? rejectionReason = null;
        foreach (var item in orderCreated.Items)
        {
            if (!byProductName.TryGetValue(item.ProductName, out var stockItem) || stockItem.QuantityAvailable < item.Quantity)
            {
                rejectionReason = $"Insufficient stock for '{item.ProductName}'.";
                break;
            }
        }

        if (rejectionReason is not null)
        {
            await publishEndpoint.Publish(new InventoryRejectedEvent(orderCreated.OrderId, rejectionReason, DateTime.UtcNow), cancellationToken);
            return;
        }

        foreach (var item in orderCreated.Items)
        {
            byProductName[item.ProductName].Reserve(item.Quantity);
        }

        await publishEndpoint.Publish(new InventoryReservedEvent(orderCreated.OrderId, DateTime.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
