using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Auth.Commands;

public class ChangePasswordHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<IAuthTokenService> _mockAuthTokenService;
    private readonly Mock<IDateTimeProvider> _mockDateTime;
    private readonly ChangePasswordHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedNow = new(2026, 5, 15, 10, 0, 0);

    public ChangePasswordHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockAuthTokenService = new Mock<IAuthTokenService>();
        _mockDateTime = new Mock<IDateTimeProvider>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _mockDateTime.Setup(x => x.UtcNow).Returns(_fixedNow);

        _handler = new ChangePasswordHandler(
            _mockUow.Object,
            _mockCurrentUser.Object,
            _mockPasswordHasher.Object,
            _mockAuthTokenService.Object,
            _mockDateTime.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldChangePasswordAndRevokeOtherSessions()
    {
        // Arrange
        var user = new User { Id = _userId, PasswordHash = "old_hash" };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockPasswordHasher.Setup(x => x.Verify("oldpass", "old_hash")).Returns(true);
        _mockPasswordHasher.Setup(x => x.Hash("NewPass123!")).Returns("new_hash");

        var command = new ChangePasswordCommand("oldpass", "NewPass123!", "refresh_token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("new_hash");
        user.UpdatedAt.Should().Be(_fixedNow);
        _mockAuthTokenService.Verify(x => x.RevokeAllSessionsAsync(_userId, "refresh_token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSocialLogin_ShouldReturnConflict()
    {
        // Arrange
        var user = new User { Id = _userId, AuthProvider = "Google" };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new ChangePasswordCommand("old", "New123!!"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Handle_WhenWrongCurrentPassword_ShouldReturnFailure()
    {
        // Arrange
        var user = new User { Id = _userId, PasswordHash = "hash" };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockPasswordHasher.Setup(x => x.Verify("wrong", "hash")).Returns(false);

        // Act
        var result = await _handler.Handle(new ChangePasswordCommand("wrong", "New123!!"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }
}
