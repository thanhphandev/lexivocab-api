using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Auth.Commands;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IJwtTokenService> _mockJwt;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly LoginCommandHandler _handler;

    private readonly User _activeUser;

    public LoginCommandHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockJwt = new Mock<IJwtTokenService>();
        _mockHasher = new Mock<IPasswordHasher>();
        _mockCache = new Mock<IDistributedCache>();

        _mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test_access_token");
        _mockJwt.Setup(j => j.GenerateRefreshToken()).Returns("test_refresh_token");
        _mockHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_rt");

        _activeUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hashed_password",
            FullName = "Test User",
            IsActive = true
        };

        _handler = new LoginCommandHandler(_mockUow.Object, _mockJwt.Object, _mockHasher.Object, _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WhenEmailNotFound_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new LoginCommand("unknown@test.com", "Password1", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.GetByEmailAsync("unknown@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Error.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Handle_WhenPasswordIncorrect_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new LoginCommand("test@test.com", "WrongPassword", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.GetByEmailAsync("test@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activeUser);
        _mockHasher.Setup(h => h.Verify("WrongPassword", "hashed_password")).Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Error.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Handle_WhenAccountDeactivated_ShouldReturnForbidden()
    {
        // Arrange
        var deactivatedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hashed_password",
            FullName = "Deactivated User",
            IsActive = false
        };

        var command = new LoginCommand("test@test.com", "Password1", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.GetByEmailAsync("test@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deactivatedUser);
        _mockHasher.Setup(h => h.Verify("Password1", "hashed_password")).Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Error.Should().Contain("deactivated");
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldReturnSuccessWithTokens()
    {
        // Arrange
        var command = new LoginCommand("  Test@Test.com  ", "CorrectPassword1", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.GetByEmailAsync("test@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activeUser);
        _mockHasher.Setup(h => h.Verify("CorrectPassword1", "hashed_password")).Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("test_access_token");
        result.Data.RefreshToken.Should().Be("test_refresh_token");
        result.Data.Email.Should().Be("test@test.com");

        // Verify LastLogin was updated
        _activeUser.LastLogin.Should().NotBeNull();

        // Verify refresh token stored in cache
        _mockCache.Verify(c => c.SetAsync(
            "rf_token:test_refresh_token",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
