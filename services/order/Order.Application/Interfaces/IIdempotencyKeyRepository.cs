using Order.Application.Domain;

namespace Order.Application.Interfaces;

public interface IIdempotencyKeyRepository
{
    Task<IdempotencyKey?> FindAsync(string key, CancellationToken cancellationToken = default);

    void Add(IdempotencyKey idempotencyKey);
}
