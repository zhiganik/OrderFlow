using Identity.Application.Dtos;
using Identity.Application.Validators;

namespace Identity.Tests.Validators;

[TestFixture]
public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _sut = new();

    [Test]
    public void ValidRequest_Passes()
    {
        var result = _sut.Validate(new RegisterRequest("user@test.com", "Password123"));

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase("not-an-email")]
    [TestCase("")]
    public void InvalidEmail_Fails(string email)
    {
        var result = _sut.Validate(new RegisterRequest(email, "Password123"));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("")]
    [TestCase("short")]
    public void InvalidPassword_Fails(string password)
    {
        var result = _sut.Validate(new RegisterRequest("user@test.com", password));

        Assert.That(result.IsValid, Is.False);
    }
}
