using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Auth.Commands;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IAuthTokenService> _mockAuthToken;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IDateTimeProvider> _mockDateTime;
    private readonly LoginCommandHandler _handler;

    private readonly User _activeUser;

    public LoginCommandHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockAuthToken = new Mock<IAuthTokenService>();
        _mockHasher = new Mock<IPasswordHasher>();
        _mockDateTime = new Mock<IDateTimeProvider>();
        _mockDateTime.Setup(d => d.UtcNow).Returns(new DateTime(2026, 5, 15, 10, 0, 0));

        _activeUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hashed_password",
            FullName = "Test User",
            IsActive = true,
            EmailConfirmed = true
        };

        _handler = new LoginCommandHandler(_mockUow.Object, _mockAuthToken.Object, _mockHasher.Object, _mockDateTime.Object);
    }

    [Fact]
    public async Task Handle_WhenEmailNotFound_ShouldReturnFailure()
    {
        // Arrange
        var command = new LoginCommand("unknown@test.com", "Password1", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Handle_WhenPasswordIncorrect_ShouldReturnFailure()
    {
        // Arrange
        var command = new LoginCommand("test@test.com", "WrongPassword", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activeUser);
        _mockHasher.Setup(h => h.Verify("WrongPassword", "hashed_password")).Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldCallAuthTokenService()
    {
        // Arrange
        var command = new LoginCommand("test@test.com", "CorrectPassword1", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activeUser);
        _mockHasher.Setup(h => h.Verify("CorrectPassword1", "hashed_password")).Returns(true);
        
        var authResponse = new AuthResponse(_activeUser.Id, _activeUser.Email, _activeUser.FullName, "User", "access", "refresh", DateTime.UtcNow.AddDays(1), null, true, true);
        _mockAuthToken.Setup(a => a.IssueTokenPairAsync(_activeUser, command.DeviceInfo, command.IpAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(authResponse);
        _mockAuthToken.Verify(a => a.IssueTokenPairAsync(_activeUser, command.DeviceInfo, command.IpAddress, It.IsAny<CancellationToken>()), Times.Once);
    }
}
