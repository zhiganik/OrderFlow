using Inventory.Application.Interfaces;
using MassTransit;
using OrderFlow.Contracts;

namespace Inventory.Infrastructure.Messaging;

public class OrderCanceledConsumer(IStockReservationService reservationService) : IConsumer<OrderCanceledEvent>
{
    public Task Consume(ConsumeContext<OrderCanceledEvent> context) =>
        reservationService.RestockAsync(context.Message, context.CancellationToken);
}
