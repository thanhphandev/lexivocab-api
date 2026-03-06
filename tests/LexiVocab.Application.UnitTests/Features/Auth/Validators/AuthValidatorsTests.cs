using FluentAssertions;
using FluentValidation.TestHelper;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Application.Features.Auth.Validators;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Auth.Validators;

public class AuthValidatorsTests
{
    // ─── RegisterCommandValidator ──────────────────────────────────

    private readonly RegisterCommandValidator _registerValidator = new();

    [Fact]
    public void RegisterValidator_WhenValid_ShouldPassAllRules()
    {
        var command = new RegisterCommand("test@email.com", "Password1", "John Doe", "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RegisterValidator_WhenEmailEmpty_ShouldFail(string? email)
    {
        var command = new RegisterCommand(email!, "Password1", "John Doe", "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void RegisterValidator_WhenEmailInvalidFormat_ShouldFail()
    {
        var command = new RegisterCommand("not-an-email", "Password1", "John Doe", "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void RegisterValidator_WhenEmailTooLong_ShouldFail()
    {
        var longEmail = new string('a', 248) + "@test.com"; // 257 chars, exceeds MaximumLength(255)
        var command = new RegisterCommand(longEmail, "Password1", "John Doe", "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RegisterValidator_WhenPasswordEmpty_ShouldFail(string? password)
    {
        var command = new RegisterCommand("test@email.com", password!, "John Doe", "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void RegisterValidator_WhenPasswordTooShort_ShouldFail()
    {
        var command = new RegisterCommand("test@email.com", "Pass1", "John Doe", "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void RegisterValidator_WhenPasswordMissingUppercase_ShouldFail()
    {
        var command = new RegisterCommand("test@email.com", "password1", "John Doe", "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void RegisterValidator_WhenPasswordMissingLowercase_ShouldFail()
    {
        var command = new RegisterCommand("test@email.com", "PASSWORD1", "John Doe", "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void RegisterValidator_WhenPasswordMissingDigit_ShouldFail()
    {
        var command = new RegisterCommand("test@email.com", "PasswordOnly", "John Doe", "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RegisterValidator_WhenFullNameEmpty_ShouldFail(string? fullName)
    {
        var command = new RegisterCommand("test@email.com", "Password1", fullName!, "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void RegisterValidator_WhenFullNameTooLong_ShouldFail()
    {
        var longName = new string('A', 101);
        var command = new RegisterCommand("test@email.com", "Password1", longName, "Chrome", "127.0.0.1");
        var result = _registerValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    // ─── LoginCommandValidator ─────────────────────────────────────

    private readonly LoginCommandValidator _loginValidator = new();

    [Fact]
    public void LoginValidator_WhenValid_ShouldPassAllRules()
    {
        var command = new LoginCommand("test@email.com", "any-password", "Chrome", "127.0.0.1");
        var result = _loginValidator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void LoginValidator_WhenEmailEmpty_ShouldFail(string? email)
    {
        var command = new LoginCommand(email!, "password", "Chrome", "127.0.0.1");
        var result = _loginValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void LoginValidator_WhenEmailInvalidFormat_ShouldFail()
    {
        var command = new LoginCommand("not-valid", "password", "Chrome", "127.0.0.1");
        var result = _loginValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void LoginValidator_WhenPasswordEmpty_ShouldFail(string? password)
    {
        var command = new LoginCommand("test@email.com", password!, "Chrome", "127.0.0.1");
        var result = _loginValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
