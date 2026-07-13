namespace OrderFlow.Contracts;

public sealed record OrderCanceledEvent(
    Guid OrderId,
    IReadOnlyList<OrderCanceledItem> Items,
    DateTime CanceledAt);

public sealed record OrderCanceledItem(string ProductName, int Quantity);
