using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace OrderFlow.Shared.Auth;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public bool IsAdmin => User?.IsInRole(AuthPolicies.Admin) ?? false;
}
