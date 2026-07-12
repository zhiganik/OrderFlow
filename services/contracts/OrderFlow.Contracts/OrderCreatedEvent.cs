namespace OrderFlow.Contracts;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyList<OrderCreatedItem> Items,
    DateTime CreatedAt);

public sealed record OrderCreatedItem(string ProductName, int Quantity);
