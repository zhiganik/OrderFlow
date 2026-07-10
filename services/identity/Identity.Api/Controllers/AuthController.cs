using Identity.Application.Dtos;
using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var outcome = await authService.RegisterAsync(request, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Result) : BadRequest(new { message = outcome.Error });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var outcome = await authService.LoginAsync(request, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Result) : Unauthorized(new { message = outcome.Error });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var outcome = await authService.RefreshAsync(request, cancellationToken);
        return outcome.Succeeded ? Ok(outcome.Result) : Unauthorized(new { message = outcome.Error });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
