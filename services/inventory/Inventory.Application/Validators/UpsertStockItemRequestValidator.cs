using FluentValidation;
using Inventory.Application.Dtos;

namespace Inventory.Application.Validators;

public class UpsertStockItemRequestValidator : AbstractValidator<UpsertStockItemRequest>
{
    public UpsertStockItemRequestValidator()
    {
        RuleFor(x => x.ProductName).NotEmpty();
        RuleFor(x => x.QuantityAvailable).GreaterThanOrEqualTo(0);
    }
}
