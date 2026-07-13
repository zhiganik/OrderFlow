namespace Order.Application.Domain;

public class Order
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public List<OrderItem> Items { get; private set; } = [];

    private Order()
    {
    }

    public static Order Create(Guid customerId, IReadOnlyCollection<(string ProductName, int Quantity)> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("An order must have at least one item.", nameof(items));
        }

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };

        order.Items = items.Select(item => OrderItem.Create(order.Id, item.ProductName, item.Quantity)).ToList();

        return order;
    }

    public void MarkReserved()
    {
        Status = OrderStatus.Reserved;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRejected(string reason)
    {
        Status = OrderStatus.Rejected;
        RejectionReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCanceled()
    {
        Status = OrderStatus.Canceled;
        UpdatedAt = DateTime.UtcNow;
    }
}
