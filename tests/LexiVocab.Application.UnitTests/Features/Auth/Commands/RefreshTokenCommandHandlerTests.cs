using System.Text;
using System.Text.Json;
using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Auth.Commands;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IJwtTokenService> _mockJwt;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly RefreshTokenCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public RefreshTokenCommandHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockJwt = new Mock<IJwtTokenService>();
        _mockHasher = new Mock<IPasswordHasher>();
        _mockCache = new Mock<IDistributedCache>();

        _mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("new_access_token");
        _mockJwt.Setup(j => j.GenerateRefreshToken()).Returns("new_refresh_token");
        _mockHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_new_rt");

        _handler = new RefreshTokenCommandHandler(_mockUow.Object, _mockJwt.Object, _mockHasher.Object, _mockCache.Object);
    }

    private void SetupCachedToken(string refreshToken, Guid userId)
    {
        var metadata = JsonSerializer.Serialize(new RefreshTokenMetadata(userId, "Chrome", "127.0.0.1", DateTime.UtcNow));
        var bytes = Encoding.UTF8.GetBytes(metadata);
        _mockCache.Setup(c => c.GetAsync($"rf_token:{refreshToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);
    }

    [Fact]
    public async Task Handle_WhenTokenNotInCache_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new RefreshTokenCommand("old_at", "expired_rt", "Chrome", "127.0.0.1");
        _mockCache.Setup(c => c.GetAsync("rf_token:expired_rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Error.Should().Contain("Invalid or expired refresh token");
    }

    [Fact]
    public async Task Handle_WhenUserDeactivatedOrDeleted_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new RefreshTokenCommand("old_at", "valid_rt", "Chrome", "127.0.0.1");
        SetupCachedToken("valid_rt", _userId);

        _mockUow.Setup(u => u.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _userId, IsActive = false, Email = "test@test.com", FullName = "Test" });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Error.Should().Contain("deactivated");
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldRotateTokensAndReturnSuccess()
    {
        // Arrange
        var activeUser = new User
        {
            Id = _userId,
            Email = "test@test.com",
            FullName = "Test User",
            IsActive = true
        };

        var command = new RefreshTokenCommand("old_at", "old_rt", "Chrome", "127.0.0.1");
        SetupCachedToken("old_rt", _userId);
        _mockUow.Setup(u => u.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data!.AccessToken.Should().Be("new_access_token");
        result.Data.RefreshToken.Should().Be("new_refresh_token");

        // Verify old token removed (rotation)
        _mockCache.Verify(c => c.RemoveAsync("rf_token:old_rt", It.IsAny<CancellationToken>()), Times.Once);

        // Verify new token stored
        _mockCache.Verify(c => c.SetAsync(
            "rf_token:new_refresh_token",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify user updated with new hash
        activeUser.RefreshTokenHash.Should().Be("hashed_new_rt");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
