using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Auth.Commands;

/// <summary>
/// Additional negative-path tests for RegisterCommandHandler.
/// </summary>
public class RegisterNegativePathTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IAuthTokenService> _mockAuthToken;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IEmailQueueService> _mockEmailQueue;
    private readonly Mock<IEmailTemplateService> _mockTemplateService;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly RegisterCommandHandler _handler;

    public RegisterNegativePathTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockAuthToken = new Mock<IAuthTokenService>();
        _mockHasher = new Mock<IPasswordHasher>();
        _mockCache = new Mock<IDistributedCache>();
        _mockEmailQueue = new Mock<IEmailQueueService>();
        _mockTemplateService = new Mock<IEmailTemplateService>();
        _mockConfig = new Mock<IConfiguration>();

        _mockConfig.Setup(c => c["Auth:RequireEmailVerification"]).Returns("false");

        _handler = new RegisterCommandHandler(
            _mockUow.Object,
            _mockAuthToken.Object,
            _mockHasher.Object,
            _mockCache.Object,
            _mockEmailQueue.Object,
            _mockTemplateService.Object,
            _mockConfig.Object,
            Mock.Of<ILogger<RegisterCommandHandler>>());
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ShouldReturnConflict()
    {
        // Arrange
        var command = new RegisterCommand("existing@test.com", "Password123!", "Duplicate User", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.EmailExistsAsync("existing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        _mockUow.Verify(u => u.Users.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>
/// Additional negative-path tests for GoogleLoginCommandHandler.
/// </summary>
public class GoogleLoginNegativePathTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IAuthTokenService> _mockAuthToken;
    private readonly Mock<IGoogleAuthService> _mockGoogleAuth;
    private readonly GoogleLoginCommandHandler _handler;

    public GoogleLoginNegativePathTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockAuthToken = new Mock<IAuthTokenService>();
        _mockGoogleAuth = new Mock<IGoogleAuthService>();

        _handler = new GoogleLoginCommandHandler(
            _mockUow.Object,
            _mockAuthToken.Object,
            _mockGoogleAuth.Object,
            Mock.Of<IEmailQueueService>(),
            Mock.Of<IEmailTemplateService>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<GoogleLoginCommandHandler>>());
    }

    [Fact]
    public async Task Handle_WhenInvalidGoogleToken_ShouldReturnUnauthorized()
    {
        // Arrange — Google returns null for invalid token
        var command = new GoogleLoginCommand("invalid_token", "Chrome", "127.0.0.1");
        _mockGoogleAuth.Setup(g => g.ValidateIdTokenAsync("invalid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoogleUserInfo?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Handle_WhenUserIsDeactivated_ShouldReturnForbidden()
    {
        // Arrange
        var command = new GoogleLoginCommand("valid_token", "Chrome", "127.0.0.1");
        var googleUser = new GoogleUserInfo("google_sub_123", "deactivated@test.com", "Banned User", null);
        _mockGoogleAuth.Setup(g => g.ValidateIdTokenAsync("valid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(googleUser);

        var deactivatedUser = new User { Id = Guid.NewGuid(), Email = "deactivated@test.com", IsActive = false };
        _mockUow.Setup(u => u.Users.GetByAuthProviderAsync("Google", "google_sub_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deactivatedUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }
}
