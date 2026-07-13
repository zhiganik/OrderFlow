using Inventory.Application.Domain;

namespace Inventory.Tests.Domain;

[TestFixture]
public class StockItemTests
{
    [Test]
    public void Create_Valid_SetsFields()
    {
        var item = StockItem.Create("Widget", 10);

        Assert.That(item.ProductName, Is.EqualTo("Widget"));
        Assert.That(item.QuantityAvailable, Is.EqualTo(10));
    }

    [Test]
    public void Create_EmptyProductName_Throws()
    {
        Assert.Throws<ArgumentException>(() => StockItem.Create("", 10));
    }

    [Test]
    public void Create_NegativeQuantity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StockItem.Create("Widget", -1));
    }

    [Test]
    public void SetQuantity_Negative_Throws()
    {
        var item = StockItem.Create("Widget", 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => item.SetQuantity(-1));
    }

    [Test]
    public void SetQuantity_Valid_UpdatesQuantity()
    {
        var item = StockItem.Create("Widget", 10);

        item.SetQuantity(25);

        Assert.That(item.QuantityAvailable, Is.EqualTo(25));
    }

    [Test]
    public void Reserve_SufficientStock_Decrements()
    {
        var item = StockItem.Create("Widget", 10);

        item.Reserve(4);

        Assert.That(item.QuantityAvailable, Is.EqualTo(6));
    }

    [Test]
    public void Reserve_MoreThanAvailable_Throws()
    {
        var item = StockItem.Create("Widget", 10);

        Assert.Throws<InvalidOperationException>(() => item.Reserve(11));
    }

    [Test]
    public void Restock_Increments()
    {
        var item = StockItem.Create("Widget", 10);

        item.Restock(5);

        Assert.That(item.QuantityAvailable, Is.EqualTo(15));
    }
}
