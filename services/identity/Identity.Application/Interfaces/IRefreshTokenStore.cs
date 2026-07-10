namespace Identity.Application.Interfaces;

public interface IRefreshTokenStore
{
    Task StoreAsync(string refreshToken, string userId, TimeSpan ttl, CancellationToken cancellationToken);

    Task<string?> GetUserIdAsync(string refreshToken, CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
