namespace Inventory.Application.Dtos;

public class UpsertStockItemRequest
{
    public string ProductName { get; set; } = string.Empty;
    public int QuantityAvailable { get; set; }
}
