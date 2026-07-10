using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Application.Domain;
using Identity.Application.Dtos;
using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Application.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    IRefreshTokenStore refreshTokenStore,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<AuthOutcome> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Registration failed for {Email}: {Reason}", request.Email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return AuthOutcome.Failure(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        logger.LogInformation("User {UserId} registered", user.Id);
        return AuthOutcome.Success(await IssueTokensAsync(user, cancellationToken));
    }

    public async Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            logger.LogWarning("Login failed for {Email}", request.Email);
            return AuthOutcome.Failure("Invalid email or password.");
        }

        logger.LogInformation("User {UserId} logged in", user.Id);
        return AuthOutcome.Success(await IssueTokensAsync(user, cancellationToken));
    }

    public async Task<AuthOutcome> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var userId = await refreshTokenStore.GetUserIdAsync(request.RefreshToken, cancellationToken);
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogWarning("Token refresh failed: invalid or expired refresh token");
            return AuthOutcome.Failure("Invalid or expired refresh token.");
        }

        await refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);

        logger.LogInformation("User {UserId} refreshed tokens", user.Id);
        return AuthOutcome.Success(await IssueTokensAsync(user, cancellationToken));
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var userId = await refreshTokenStore.GetUserIdAsync(request.RefreshToken, cancellationToken);

        await refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);

        logger.LogInformation("User {UserId} logged out", userId);
    }

    private async Task<AuthResult> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        await refreshTokenStore.StoreAsync(
            refreshToken,
            user.Id,
            TimeSpan.FromDays(_jwtOptions.RefreshTokenDays),
            cancellationToken);

        return new AuthResult(accessToken, refreshToken, expiresAt);
    }
}
