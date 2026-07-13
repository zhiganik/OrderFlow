using Order.Application.Domain;

namespace Order.Tests.Domain;

[TestFixture]
public class OrderTests
{
    private static readonly List<(string ProductName, int Quantity)> OneItem =
        [("Widget", 2)];

    [Test]
    public void Create_WithItems_IsPendingWithMatchingItems()
    {
        var customerId = Guid.NewGuid();

        var order = OrderEntity.Create(customerId, OneItem);

        Assert.That(order.CustomerId, Is.EqualTo(customerId));
        Assert.That(order.Status, Is.EqualTo(OrderStatus.Pending));
        Assert.That(order.Items, Has.Count.EqualTo(1));
        Assert.That(order.Items[0].ProductName, Is.EqualTo("Widget"));
        Assert.That(order.Items[0].Quantity, Is.EqualTo(2));
    }

    [Test]
    public void Create_NoItems_Throws()
    {
        Assert.Throws<ArgumentException>(() => OrderEntity.Create(Guid.NewGuid(), []));
    }

    [Test]
    public void MarkReserved_SetsStatusReservedAndUpdatesTimestamp()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), OneItem);
        var before = order.UpdatedAt;

        order.MarkReserved();

        Assert.That(order.Status, Is.EqualTo(OrderStatus.Reserved));
        Assert.That(order.UpdatedAt, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public void MarkRejected_SetsStatusRejectedWithReason()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), OneItem);

        order.MarkRejected("Out of stock.");

        Assert.That(order.Status, Is.EqualTo(OrderStatus.Rejected));
        Assert.That(order.RejectionReason, Is.EqualTo("Out of stock."));
    }
}
