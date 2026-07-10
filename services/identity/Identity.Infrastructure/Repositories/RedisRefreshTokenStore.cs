using Identity.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace Identity.Infrastructure.Repositories;

public class RedisRefreshTokenStore(IDistributedCache cache) : IRefreshTokenStore
{
    private static string KeyFor(string refreshToken) => $"refresh-token:{refreshToken}";

    public Task StoreAsync(string refreshToken, string userId, TimeSpan ttl, CancellationToken cancellationToken)
        => cache.SetStringAsync(
            KeyFor(refreshToken),
            userId,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken);

    public Task<string?> GetUserIdAsync(string refreshToken, CancellationToken cancellationToken)
        => cache.GetStringAsync(KeyFor(refreshToken), cancellationToken);

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
        => cache.RemoveAsync(KeyFor(refreshToken), cancellationToken);
}
