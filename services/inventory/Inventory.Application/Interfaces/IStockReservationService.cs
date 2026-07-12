using OrderFlow.Contracts;

namespace Inventory.Application.Interfaces;

public interface IStockReservationService
{
    Task ReserveOrRejectAsync(OrderCreatedEvent orderCreated, CancellationToken cancellationToken = default);
}
