using Inventory.Application.Dtos;
using Inventory.Application.Validators;

namespace Inventory.Tests.Validators;

[TestFixture]
public class UpsertStockItemRequestValidatorTests
{
    private readonly UpsertStockItemRequestValidator _sut = new();

    [Test]
    public void ValidRequest_Passes()
    {
        var result = _sut.Validate(new UpsertStockItemRequest { ProductName = "Widget", QuantityAvailable = 5 });

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void EmptyProductName_Fails()
    {
        var result = _sut.Validate(new UpsertStockItemRequest { ProductName = "", QuantityAvailable = 5 });

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void NegativeQuantity_Fails()
    {
        var result = _sut.Validate(new UpsertStockItemRequest { ProductName = "Widget", QuantityAvailable = -1 });

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ZeroQuantity_Passes()
    {
        var result = _sut.Validate(new UpsertStockItemRequest { ProductName = "Widget", QuantityAvailable = 0 });

        Assert.That(result.IsValid, Is.True);
    }
}
