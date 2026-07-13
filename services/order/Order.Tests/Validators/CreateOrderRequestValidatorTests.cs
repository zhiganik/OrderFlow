using Order.Application.Dtos;
using Order.Application.Validators;

namespace Order.Tests.Validators;

[TestFixture]
public class CreateOrderRequestValidatorTests
{
    private readonly CreateOrderRequestValidator _sut = new();

    [Test]
    public void ValidRequest_Passes()
    {
        var request = new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductName = "Widget", Quantity = 1 }],
        };

        var result = _sut.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void EmptyItems_Fails()
    {
        var request = new CreateOrderRequest { Items = [] };

        var result = _sut.Validate(request);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ItemWithEmptyProductName_Fails()
    {
        var request = new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductName = "", Quantity = 1 }],
        };

        var result = _sut.Validate(request);

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ItemWithNonPositiveQuantity_Fails(int quantity)
    {
        var request = new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductName = "Widget", Quantity = quantity }],
        };

        var result = _sut.Validate(request);

        Assert.That(result.IsValid, Is.False);
    }
}
