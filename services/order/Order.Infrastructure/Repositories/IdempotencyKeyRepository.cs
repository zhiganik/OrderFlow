using Microsoft.EntityFrameworkCore;
using Order.Application.Domain;
using Order.Application.Interfaces;
using Order.Infrastructure.Persistence;

namespace Order.Infrastructure.Repositories;

public class IdempotencyKeyRepository(OrderDbContext context) : IIdempotencyKeyRepository
{
    public Task<IdempotencyKey?> FindAsync(string key, CancellationToken cancellationToken = default) =>
        context.IdempotencyKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Key == key, cancellationToken);

    public void Add(IdempotencyKey idempotencyKey) => context.IdempotencyKeys.Add(idempotencyKey);
}
