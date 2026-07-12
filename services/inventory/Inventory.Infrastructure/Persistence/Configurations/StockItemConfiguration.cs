using Inventory.Application.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("StockItems");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(s => s.ProductName).IsUnique();
        builder.HasIndex(s => s.CreatedAt);
    }
}
