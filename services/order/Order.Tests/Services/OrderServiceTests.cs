using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Order.Application.Domain;
using Order.Application.Dtos;
using Order.Application.Interfaces;
using Order.Application.Services;

namespace Order.Tests.Services;

[TestFixture]
public class OrderServiceTests
{
    private Mock<IOrderRepository> _orderRepository = null!;
    private Mock<IIdempotencyKeyRepository> _idempotencyKeyRepository = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private Mock<IPublishEndpoint> _publishEndpoint = null!;
    private OrderService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _orderRepository = new Mock<IOrderRepository>();
        _idempotencyKeyRepository = new Mock<IIdempotencyKeyRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _publishEndpoint = new Mock<IPublishEndpoint>();

        _sut = new OrderService(
            _orderRepository.Object,
            _idempotencyKeyRepository.Object,
            _unitOfWork.Object,
            _publishEndpoint.Object,
            Mock.Of<ILogger<OrderService>>());
    }

    private static CreateOrderRequest OneItemRequest(string productName = "Widget", int quantity = 2) =>
        new() { Items = [new CreateOrderItemRequest { ProductName = productName, Quantity = quantity }] };

    [Test]
    public async Task CreateOrderAsync_NewIdempotencyKey_PersistsAndPublishes()
    {
        var customerId = Guid.NewGuid();
        _idempotencyKeyRepository.Setup(r => r.FindAsync("key-1", It.IsAny<CancellationToken>())).ReturnsAsync((IdempotencyKey?)null);

        var result = await _sut.CreateOrderAsync(OneItemRequest(), customerId, "key-1", CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(CreateOrderOutcome.Created));
        Assert.That(result.StatusCode, Is.EqualTo(201));
        Assert.That(result.Order!.CustomerId, Is.EqualTo(customerId));
        _orderRepository.Verify(r => r.Add(It.IsAny<OrderEntity>()), Times.Once);
        _idempotencyKeyRepository.Verify(r => r.Add(It.IsAny<IdempotencyKey>()), Times.Once);
        _publishEndpoint.Verify(p => p.Publish(It.IsAny<OrderFlow.Contracts.OrderCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateOrderAsync_RetriedWithSameKeyAndBody_ReplaysCachedResponse()
    {
        var customerId = Guid.NewGuid();
        var request = OneItemRequest();
        IdempotencyKey? captured = null;
        _idempotencyKeyRepository.Setup(r => r.Add(It.IsAny<IdempotencyKey>())).Callback<IdempotencyKey>(k => captured = k);
        _idempotencyKeyRepository.Setup(r => r.FindAsync("key-1", It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var first = await _sut.CreateOrderAsync(request, customerId, "key-1", CancellationToken.None);

        _idempotencyKeyRepository.Setup(r => r.FindAsync("key-1", It.IsAny<CancellationToken>())).ReturnsAsync(captured);

        var second = await _sut.CreateOrderAsync(request, customerId, "key-1", CancellationToken.None);

        Assert.That(second.Outcome, Is.EqualTo(CreateOrderOutcome.ReplayedFromCache));
        Assert.That(second.Order!.Id, Is.EqualTo(first.Order!.Id));
        _orderRepository.Verify(r => r.Add(It.IsAny<OrderEntity>()), Times.Once);
        _publishEndpoint.Verify(p => p.Publish(It.IsAny<OrderFlow.Contracts.OrderCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateOrderAsync_RetriedWithSameKeyDifferentBody_ReturnsConflict()
    {
        var customerId = Guid.NewGuid();
        IdempotencyKey? captured = null;
        _idempotencyKeyRepository.Setup(r => r.Add(It.IsAny<IdempotencyKey>())).Callback<IdempotencyKey>(k => captured = k);
        _idempotencyKeyRepository.Setup(r => r.FindAsync("key-1", It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        await _sut.CreateOrderAsync(OneItemRequest("Widget", 2), customerId, "key-1", CancellationToken.None);

        _idempotencyKeyRepository.Setup(r => r.FindAsync("key-1", It.IsAny<CancellationToken>())).ReturnsAsync(captured);

        var result = await _sut.CreateOrderAsync(OneItemRequest("Gadget", 5), customerId, "key-1", CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(CreateOrderOutcome.Conflict));
        Assert.That(result.StatusCode, Is.EqualTo(409));
        _orderRepository.Verify(r => r.Add(It.IsAny<OrderEntity>()), Times.Once);
    }

    [Test]
    public async Task GetOrdersByCustomerAsync_ClampsPageAndPageSize()
    {
        var customerId = Guid.NewGuid();
        _orderRepository.Setup(r => r.GetPagedByCustomerIdAsync(customerId, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderEntity>(), 0));

        var result = await _sut.GetOrdersByCustomerAsync(customerId, page: 0, pageSize: 500, CancellationToken.None);

        Assert.That(result.Page, Is.EqualTo(1));
        Assert.That(result.PageSize, Is.EqualTo(100));
        _orderRepository.Verify(r => r.GetPagedByCustomerIdAsync(customerId, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetOrderByIdAsync_NotFound_ReturnsNull()
    {
        _orderRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrderEntity?)null);

        var result = await _sut.GetOrderByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetOrderByIdAsync_Found_ReturnsMappedResponse()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), [("Widget", 1)]);
        _orderRepository.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.GetOrderByIdAsync(order.Id, CancellationToken.None);

        Assert.That(result!.Id, Is.EqualTo(order.Id));
        Assert.That(result.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MarkReservedAsync_OrderFound_TransitionsAndSaves()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), [("Widget", 1)]);
        _orderRepository.Setup(r => r.FindByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        await _sut.MarkReservedAsync(order.Id, CancellationToken.None);

        Assert.That(order.Status, Is.EqualTo(OrderStatus.Reserved));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task MarkReservedAsync_OrderNotFound_DoesNotSave()
    {
        _orderRepository.Setup(r => r.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrderEntity?)null);

        await _sut.MarkReservedAsync(Guid.NewGuid(), CancellationToken.None);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task MarkRejectedAsync_OrderFound_TransitionsWithReasonAndSaves()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), [("Widget", 1)]);
        _orderRepository.Setup(r => r.FindByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        await _sut.MarkRejectedAsync(order.Id, "Insufficient stock.", CancellationToken.None);

        Assert.That(order.Status, Is.EqualTo(OrderStatus.Rejected));
        Assert.That(order.RejectionReason, Is.EqualTo("Insufficient stock."));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task MarkRejectedAsync_OrderNotFound_DoesNotSave()
    {
        _orderRepository.Setup(r => r.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrderEntity?)null);

        await _sut.MarkRejectedAsync(Guid.NewGuid(), "reason", CancellationToken.None);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CancelOrderAsync_ReservedOrder_CancelsAndPublishesEvent()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), [("Widget", 2)]);
        order.MarkReserved();
        _orderRepository.Setup(r => r.FindByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.CancelOrderAsync(order.Id, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(CancelOrderOutcome.Canceled));
        Assert.That(order.Status, Is.EqualTo(OrderStatus.Canceled));
        _publishEndpoint.Verify(p => p.Publish(
            It.Is<OrderFlow.Contracts.OrderCanceledEvent>(e => e.OrderId == order.Id && e.Items.Single().Quantity == 2),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CancelOrderAsync_OrderNotFound_ReturnsNotFound()
    {
        _orderRepository.Setup(r => r.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrderEntity?)null);

        var result = await _sut.CancelOrderAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(CancelOrderOutcome.NotFound));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(OrderStatus.Pending)]
    [TestCase(OrderStatus.Rejected)]
    [TestCase(OrderStatus.Canceled)]
    public async Task CancelOrderAsync_NotReserved_ReturnsInvalidStatusWithoutPublishingOrSaving(OrderStatus status)
    {
        var order = OrderEntity.Create(Guid.NewGuid(), [("Widget", 2)]);
        switch (status)
        {
            case OrderStatus.Rejected:
                order.MarkRejected("reason");
                break;
            case OrderStatus.Canceled:
                order.MarkCanceled();
                break;
        }

        _orderRepository.Setup(r => r.FindByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _sut.CancelOrderAsync(order.Id, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(CancelOrderOutcome.InvalidStatus));
        _publishEndpoint.Verify(p => p.Publish(It.IsAny<OrderFlow.Contracts.OrderCanceledEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
