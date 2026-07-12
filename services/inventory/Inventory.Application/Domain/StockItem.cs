namespace Inventory.Application.Domain;

public class StockItem
{
    public Guid Id { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int QuantityAvailable { get; private set; }

    private StockItem()
    {
    }

    public static StockItem Create(string productName, int quantityAvailable)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Product name is required.", nameof(productName));
        }

        if (quantityAvailable < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityAvailable), "Quantity available cannot be negative.");
        }

        return new StockItem
        {
            Id = Guid.NewGuid(),
            ProductName = productName,
            QuantityAvailable = quantityAvailable,
        };
    }

    public void SetQuantity(int quantityAvailable)
    {
        if (quantityAvailable < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityAvailable), "Quantity available cannot be negative.");
        }

        QuantityAvailable = quantityAvailable;
    }
}
