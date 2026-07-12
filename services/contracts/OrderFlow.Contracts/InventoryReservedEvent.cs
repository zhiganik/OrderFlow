namespace OrderFlow.Contracts;

public sealed record InventoryReservedEvent(Guid OrderId, DateTime ReservedAt);
