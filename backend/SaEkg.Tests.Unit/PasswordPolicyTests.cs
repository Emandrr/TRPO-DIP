using FluentAssertions;
using SaEkg.Application.Auth;
using Xunit;

namespace SaEkg.Tests.Unit;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("short1")]
    [InlineData("allletters")]
    [InlineData("12345678")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_passwords(string? password)
    {
        PasswordPolicy.IsValid(password, out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("Password1")]
    [InlineData("абвгде12")]
    [InlineData("GoodPass9")]
    public void Accepts_valid_passwords(string password)
    {
        PasswordPolicy.IsValid(password, out var error).Should().BeTrue();
        error.Should().BeNull();
    }
}
