using Inventory.Application.Interfaces;
using MassTransit;
using OrderFlow.Contracts;

namespace Inventory.Infrastructure.Messaging;

public class OrderCreatedConsumer(IStockReservationService reservationService) : IConsumer<OrderCreatedEvent>
{
    public Task Consume(ConsumeContext<OrderCreatedEvent> context) =>
        reservationService.ReserveOrRejectAsync(context.Message, context.CancellationToken);
}
