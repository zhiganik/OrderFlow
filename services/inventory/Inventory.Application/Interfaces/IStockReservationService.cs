using OrderFlow.Contracts;

namespace Inventory.Application.Interfaces;

public interface IStockReservationService
{
    Task ReserveOrRejectAsync(OrderCreatedEvent orderCreated, CancellationToken cancellationToken = default);

    Task RestockAsync(OrderCanceledEvent orderCanceled, CancellationToken cancellationToken = default);
}
