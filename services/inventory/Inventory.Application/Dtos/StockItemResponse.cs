namespace Inventory.Application.Dtos;

public class StockItemResponse
{
    public required Guid Id { get; init; }
    public required string ProductName { get; init; }
    public required int QuantityAvailable { get; init; }
}
