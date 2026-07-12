using MassTransit;
using Order.Application.Interfaces;
using OrderFlow.Contracts;

namespace Order.Infrastructure.Messaging;

public class InventoryRejectedConsumer(IOrderService orderService) : IConsumer<InventoryRejectedEvent>
{
    public Task Consume(ConsumeContext<InventoryRejectedEvent> context) =>
        orderService.MarkRejectedAsync(context.Message.OrderId, context.Message.Reason, context.CancellationToken);
}
