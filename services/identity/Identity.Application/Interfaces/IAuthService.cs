using Identity.Application.Dtos;

namespace Identity.Application.Interfaces;

public interface IAuthService
{
    Task<AuthOutcome> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthOutcome> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken);
}
