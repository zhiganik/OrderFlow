using Inventory.Application.Domain;
using Inventory.Application.Interfaces;
using Inventory.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using OrderFlow.Contracts;

namespace Inventory.Tests.Services;

[TestFixture]
public class StockReservationServiceTests
{
    private Mock<IStockItemRepository> _repository = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private Mock<IPublishEndpoint> _publishEndpoint = null!;
    private StockReservationService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IStockItemRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _publishEndpoint = new Mock<IPublishEndpoint>();
        _sut = new StockReservationService(_repository.Object, _unitOfWork.Object, _publishEndpoint.Object, Mock.Of<ILogger<StockReservationService>>());
    }

    private static OrderCreatedEvent OrderCreated(params (string ProductName, int Quantity)[] items) =>
        new(Guid.NewGuid(), Guid.NewGuid(), items.Select(i => new OrderCreatedItem(i.ProductName, i.Quantity)).ToList(), DateTime.UtcNow);

    private static OrderCanceledEvent OrderCanceled(params (string ProductName, int Quantity)[] items) =>
        new(Guid.NewGuid(), items.Select(i => new OrderCanceledItem(i.ProductName, i.Quantity)).ToList(), DateTime.UtcNow);

    [Test]
    public async Task ReserveOrRejectAsync_SufficientStock_DecrementsAndPublishesReserved()
    {
        var widget = StockItem.Create("Widget", 10);
        _repository.Setup(r => r.FindByProductNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([widget]);

        var orderCreated = OrderCreated(("Widget", 3));

        await _sut.ReserveOrRejectAsync(orderCreated, CancellationToken.None);

        Assert.That(widget.QuantityAvailable, Is.EqualTo(7));
        _publishEndpoint.Verify(p => p.Publish(It.Is<InventoryReservedEvent>(e => e.OrderId == orderCreated.OrderId), It.IsAny<CancellationToken>()), Times.Once);
        _publishEndpoint.Verify(p => p.Publish(It.IsAny<InventoryRejectedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ReserveOrRejectAsync_ProductNotInCatalog_PublishesRejectedWithoutSaving()
    {
        _repository.Setup(r => r.FindByProductNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var orderCreated = OrderCreated(("Unknown", 1));

        await _sut.ReserveOrRejectAsync(orderCreated, CancellationToken.None);

        _publishEndpoint.Verify(p => p.Publish(It.Is<InventoryRejectedEvent>(e => e.OrderId == orderCreated.OrderId && e.Reason.Contains("Unknown")), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReserveOrRejectAsync_InsufficientQuantity_PublishesRejected()
    {
        var widget = StockItem.Create("Widget", 2);
        _repository.Setup(r => r.FindByProductNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([widget]);

        var orderCreated = OrderCreated(("Widget", 5));

        await _sut.ReserveOrRejectAsync(orderCreated, CancellationToken.None);

        Assert.That(widget.QuantityAvailable, Is.EqualTo(2), "stock must be untouched when the order is rejected");
        _publishEndpoint.Verify(p => p.Publish(It.IsAny<InventoryRejectedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ReserveOrRejectAsync_OneOfManyItemsShort_RejectsWholeOrderWithNoPartialReservation()
    {
        var widget = StockItem.Create("Widget", 10);
        var gadget = StockItem.Create("Gadget", 1);
        _repository.Setup(r => r.FindByProductNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([widget, gadget]);

        var orderCreated = OrderCreated(("Widget", 3), ("Gadget", 5));

        await _sut.ReserveOrRejectAsync(orderCreated, CancellationToken.None);

        Assert.That(widget.QuantityAvailable, Is.EqualTo(10), "no item should be decremented once any item is rejected");
        Assert.That(gadget.QuantityAvailable, Is.EqualTo(1));
        _publishEndpoint.Verify(p => p.Publish(It.IsAny<InventoryRejectedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _publishEndpoint.Verify(p => p.Publish(It.IsAny<InventoryReservedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RestockAsync_KnownProduct_IncrementsQuantityAndSaves()
    {
        var widget = StockItem.Create("Widget", 5);
        _repository.Setup(r => r.FindByProductNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([widget]);

        await _sut.RestockAsync(OrderCanceled(("Widget", 3)), CancellationToken.None);

        Assert.That(widget.QuantityAvailable, Is.EqualTo(8));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RestockAsync_UnknownProduct_SkipsItWithoutThrowing()
    {
        _repository.Setup(r => r.FindByProductNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Assert.DoesNotThrowAsync(() => _sut.RestockAsync(OrderCanceled(("Unknown", 3)), CancellationToken.None));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
