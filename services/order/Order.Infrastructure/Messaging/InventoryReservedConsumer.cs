using MassTransit;
using Order.Application.Interfaces;
using OrderFlow.Contracts;

namespace Order.Infrastructure.Messaging;

public class InventoryReservedConsumer(IOrderService orderService) : IConsumer<InventoryReservedEvent>
{
    public Task Consume(ConsumeContext<InventoryReservedEvent> context) =>
        orderService.MarkReservedAsync(context.Message.OrderId, context.CancellationToken);
}
