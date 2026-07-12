namespace OrderFlow.Contracts;

public sealed record InventoryRejectedEvent(Guid OrderId, string Reason, DateTime RejectedAt);
