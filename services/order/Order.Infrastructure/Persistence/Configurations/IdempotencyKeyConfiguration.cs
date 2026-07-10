using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Application.Domain;

namespace Order.Infrastructure.Persistence.Configurations;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("IdempotencyKeys");

        // The Idempotency-Key header value itself is the primary key —
        // the PK already gives us the unique index the lookup needs.
        builder.HasKey(k => k.Key);
        builder.Property(k => k.Key).HasMaxLength(200);

        builder.Property(k => k.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.ResponseBody).IsRequired();
    }
}
