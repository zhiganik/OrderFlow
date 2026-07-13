using Inventory.Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;

namespace Inventory.Application.Services;

public class StockReservationService(
    IStockItemRepository stockItemRepository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    ILogger<StockReservationService> logger) : IStockReservationService
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

    public async Task RestockAsync(OrderCanceledEvent orderCanceled, CancellationToken cancellationToken = default)
    {
        var productNames = orderCanceled.Items.Select(i => i.ProductName).Distinct().ToList();
        var stockItems = await stockItemRepository.FindByProductNamesAsync(productNames, cancellationToken);
        var byProductName = stockItems.ToDictionary(s => s.ProductName);

        foreach (var item in orderCanceled.Items)
        {
            if (!byProductName.TryGetValue(item.ProductName, out var stockItem))
            {
                logger.LogWarning("Cannot restock unknown product '{ProductName}' for canceled order {OrderId}", item.ProductName, orderCanceled.OrderId);
                continue;
            }

            stockItem.Restock(item.Quantity);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
