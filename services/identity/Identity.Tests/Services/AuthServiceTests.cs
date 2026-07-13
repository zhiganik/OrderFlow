using Identity.Application.Domain;
using Identity.Application.Dtos;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Identity.Tests.Services;

[TestFixture]
public class AuthServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private Mock<IRefreshTokenStore> _refreshTokenStore = null!;
    private AuthService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _userManager = CreateUserManagerMock();
        _refreshTokenStore = new Mock<IRefreshTokenStore>();

        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "OrderFlow",
            Audience = "OrderFlow.Clients",
            SigningKey = Convert.ToBase64String(new byte[32]),
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        });

        _sut = new AuthService(_userManager.Object, _refreshTokenStore.Object, jwtOptions, Mock.Of<ILogger<AuthService>>());
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    [Test]
    public async Task RegisterAsync_ValidRequest_IssuesTokensAndStoresRefreshToken()
    {
        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Password123"))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync([]);

        var outcome = await _sut.RegisterAsync(new RegisterRequest("user@test.com", "Password123"), CancellationToken.None);

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.Result!.AccessToken, Is.Not.Empty);
        Assert.That(outcome.Result.RefreshToken, Is.Not.Empty);
        _refreshTokenStore.Verify(s => s.StoreAsync(outcome.Result.RefreshToken, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RegisterAsync_UserManagerFails_ReturnsFailureOutcome()
    {
        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email already taken." }));

        var outcome = await _sut.RegisterAsync(new RegisterRequest("user@test.com", "Password123"), CancellationToken.None);

        Assert.That(outcome.Succeeded, Is.False);
        Assert.That(outcome.Error, Does.Contain("Email already taken."));
        _refreshTokenStore.Verify(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LoginAsync_UnknownEmail_ReturnsFailure()
    {
        _userManager.Setup(m => m.FindByEmailAsync("missing@test.com")).ReturnsAsync((ApplicationUser?)null);

        var outcome = await _sut.LoginAsync(new LoginRequest("missing@test.com", "Password123"), CancellationToken.None);

        Assert.That(outcome.Succeeded, Is.False);
        Assert.That(outcome.Error, Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public async Task LoginAsync_WrongPassword_ReturnsFailure()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "user@test.com" };
        _userManager.Setup(m => m.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.CheckPasswordAsync(user, "WrongPassword")).ReturnsAsync(false);

        var outcome = await _sut.LoginAsync(new LoginRequest("user@test.com", "WrongPassword"), CancellationToken.None);

        Assert.That(outcome.Succeeded, Is.False);
        Assert.That(outcome.Error, Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public async Task LoginAsync_ValidCredentials_IssuesTokens()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "user@test.com" };
        _userManager.Setup(m => m.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.CheckPasswordAsync(user, "Password123")).ReturnsAsync(true);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Admin"]);

        var outcome = await _sut.LoginAsync(new LoginRequest("user@test.com", "Password123"), CancellationToken.None);

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.Result!.AccessToken, Is.Not.Empty);
    }

    [Test]
    public async Task RefreshAsync_UnknownToken_ReturnsFailure()
    {
        _refreshTokenStore.Setup(s => s.GetUserIdAsync("bad-token", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var outcome = await _sut.RefreshAsync(new RefreshRequest("bad-token"), CancellationToken.None);

        Assert.That(outcome.Succeeded, Is.False);
        Assert.That(outcome.Error, Is.EqualTo("Invalid or expired refresh token."));
    }

    [Test]
    public async Task RefreshAsync_UserNoLongerExists_ReturnsFailure()
    {
        _refreshTokenStore.Setup(s => s.GetUserIdAsync("old-token", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        _userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync((ApplicationUser?)null);

        var outcome = await _sut.RefreshAsync(new RefreshRequest("old-token"), CancellationToken.None);

        Assert.That(outcome.Succeeded, Is.False);
        Assert.That(outcome.Error, Is.EqualTo("Invalid or expired refresh token."));
    }

    [Test]
    public async Task RefreshAsync_ValidToken_RevokesOldTokenAndIssuesNewOne()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "user@test.com" };
        _refreshTokenStore.Setup(s => s.GetUserIdAsync("old-token", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        _userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync([]);

        var outcome = await _sut.RefreshAsync(new RefreshRequest("old-token"), CancellationToken.None);

        Assert.That(outcome.Succeeded, Is.True);
        _refreshTokenStore.Verify(s => s.RevokeAsync("old-token", It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenStore.Verify(s => s.StoreAsync(outcome.Result!.RefreshToken, "user-1", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LogoutAsync_RevokesTheToken()
    {
        _refreshTokenStore.Setup(s => s.GetUserIdAsync("token", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");

        await _sut.LogoutAsync(new RefreshRequest("token"), CancellationToken.None);

        _refreshTokenStore.Verify(s => s.RevokeAsync("token", It.IsAny<CancellationToken>()), Times.Once);
    }
}
