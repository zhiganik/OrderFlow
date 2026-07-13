using Identity.Application.Dtos;
using Identity.Application.Validators;

namespace Identity.Tests.Validators;

[TestFixture]
public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _sut = new();

    [Test]
    public void ValidRequest_Passes()
    {
        var result = _sut.Validate(new LoginRequest("user@test.com", "anything"));

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase("not-an-email")]
    [TestCase("")]
    public void InvalidEmail_Fails(string email)
    {
        var result = _sut.Validate(new LoginRequest(email, "anything"));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void EmptyPassword_Fails()
    {
        var result = _sut.Validate(new LoginRequest("user@test.com", ""));

        Assert.That(result.IsValid, Is.False);
    }
}
