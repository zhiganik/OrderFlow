using Inventory.Application.Domain;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Application.Services;
using Moq;

namespace Inventory.Tests.Services;

[TestFixture]
public class StockServiceTests
{
    private Mock<IStockItemRepository> _repository = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private StockService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IStockItemRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _sut = new StockService(_repository.Object, _unitOfWork.Object);
    }

    [Test]
    public async Task SearchAsync_ClampsPageAndPageSize()
    {
        _repository.Setup(r => r.SearchAsync(1, 100, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<StockItem>(), 0));

        var result = await _sut.SearchAsync(page: 0, pageSize: 500, id: null, productName: null, CancellationToken.None);

        Assert.That(result.Page, Is.EqualTo(1));
        Assert.That(result.PageSize, Is.EqualTo(100));
        _repository.Verify(r => r.SearchAsync(1, 100, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SearchAsync_MapsItemsToResponses()
    {
        var item = StockItem.Create("Widget", 5);
        _repository.Setup(r => r.SearchAsync(1, 20, null, "Widget", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<StockItem> { item }, 1));

        var result = await _sut.SearchAsync(1, 20, null, "Widget", CancellationToken.None);

        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Items[0].ProductName, Is.EqualTo("Widget"));
    }

    [Test]
    public async Task UpsertAsync_ExistingProduct_UpdatesQuantityWithoutAdding()
    {
        var existing = StockItem.Create("Widget", 5);
        _repository.Setup(r => r.FindByProductNameAsync("Widget", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var response = await _sut.UpsertAsync(new UpsertStockItemRequest { ProductName = "Widget", QuantityAvailable = 20 }, CancellationToken.None);

        Assert.That(response.QuantityAvailable, Is.EqualTo(20));
        Assert.That(existing.QuantityAvailable, Is.EqualTo(20));
        _repository.Verify(r => r.Add(It.IsAny<StockItem>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpsertAsync_NewProduct_AddsIt()
    {
        _repository.Setup(r => r.FindByProductNameAsync("Gadget", It.IsAny<CancellationToken>())).ReturnsAsync((StockItem?)null);

        var response = await _sut.UpsertAsync(new UpsertStockItemRequest { ProductName = "Gadget", QuantityAvailable = 15 }, CancellationToken.None);

        Assert.That(response.ProductName, Is.EqualTo("Gadget"));
        Assert.That(response.QuantityAvailable, Is.EqualTo(15));
        _repository.Verify(r => r.Add(It.IsAny<StockItem>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
