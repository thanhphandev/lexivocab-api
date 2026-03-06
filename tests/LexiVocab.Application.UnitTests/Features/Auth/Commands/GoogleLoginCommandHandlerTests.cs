using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Auth.Commands;

public class GoogleLoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IJwtTokenService> _mockJwt;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IGoogleAuthService> _mockGoogleAuth;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IEmailQueueService> _mockEmailQueue;
    private readonly Mock<IEmailTemplateService> _mockTemplateService;
    private readonly GoogleLoginCommandHandler _handler;

    private readonly GoogleUserInfo _googleUser = new("google_sub_123", "google@test.com", "Google User", null);

    public GoogleLoginCommandHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockJwt = new Mock<IJwtTokenService>();
        _mockHasher = new Mock<IPasswordHasher>();
        _mockGoogleAuth = new Mock<IGoogleAuthService>();
        _mockCache = new Mock<IDistributedCache>();
        _mockEmailQueue = new Mock<IEmailQueueService>();
        _mockTemplateService = new Mock<IEmailTemplateService>();

        var config = new Mock<IConfiguration>();
        config.Setup(c => c["App:Url"]).Returns("https://test.lexivocab.store");

        _mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test_access_token");
        _mockJwt.Setup(j => j.GenerateRefreshToken()).Returns("test_refresh_token");
        _mockHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_rt");
        _mockTemplateService
            .Setup(t => t.RenderTemplateAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync("<html>Welcome</html>");

        _handler = new GoogleLoginCommandHandler(
            _mockUow.Object, _mockJwt.Object, _mockHasher.Object,
            _mockGoogleAuth.Object, _mockCache.Object, _mockEmailQueue.Object,
            _mockTemplateService.Object, config.Object);
    }

    [Fact]
    public async Task Handle_WhenInvalidGoogleToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new GoogleLoginCommand("invalid_token", "Chrome", "127.0.0.1");
        _mockGoogleAuth.Setup(g => g.ValidateIdTokenAsync("invalid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoogleUserInfo?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Error.Should().Contain("Invalid Google ID token");
    }

    [Fact]
    public async Task Handle_WhenExistingGoogleUser_ShouldReturnSuccess()
    {
        // Arrange
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "google@test.com",
            FullName = "Google User",
            AuthProvider = "Google",
            AuthProviderId = "google_sub_123",
            IsActive = true
        };

        var command = new GoogleLoginCommand("valid_token", "Chrome", "127.0.0.1");
        _mockGoogleAuth.Setup(g => g.ValidateIdTokenAsync("valid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_googleUser);
        _mockUow.Setup(u => u.Users.GetByAuthProviderAsync("Google", "google_sub_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data!.Email.Should().Be("google@test.com");

        // Should NOT create a new user
        _mockUow.Verify(u => u.Users.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenExistingEmailAccount_ShouldAutoLinkGoogleProvider()
    {
        // Arrange
        var existingEmailUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "google@test.com",
            FullName = "Email User",
            PasswordHash = "some_hash",
            IsActive = true
        };

        var command = new GoogleLoginCommand("valid_token", "Chrome", "127.0.0.1");
        _mockGoogleAuth.Setup(g => g.ValidateIdTokenAsync("valid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_googleUser);
        _mockUow.Setup(u => u.Users.GetByAuthProviderAsync("Google", "google_sub_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mockUow.Setup(u => u.Users.GetByEmailAsync("google@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEmailUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingEmailUser.AuthProvider.Should().Be("Google");
        existingEmailUser.AuthProviderId.Should().Be("google_sub_123");

        // Should NOT create a new user (linked existing)
        _mockUow.Verify(u => u.Users.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNewGoogleUser_ShouldCreateAccountAndSendWelcomeEmail()
    {
        // Arrange
        var command = new GoogleLoginCommand("valid_token", "Chrome", "127.0.0.1");
        _mockGoogleAuth.Setup(g => g.ValidateIdTokenAsync("valid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_googleUser);
        _mockUow.Setup(u => u.Users.GetByAuthProviderAsync("Google", "google_sub_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mockUow.Setup(u => u.Users.GetByEmailAsync("google@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        User? savedUser = null;
        _mockUow.Setup(u => u.Users.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => savedUser = u);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        savedUser.Should().NotBeNull();
        savedUser!.Email.Should().Be("google@test.com");
        savedUser.AuthProvider.Should().Be("Google");
        savedUser.AuthProviderId.Should().Be("google_sub_123");

        _mockEmailQueue.Verify(e => e.EnqueueEmail(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("Welcome")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAccountDeactivated_ShouldReturnForbidden()
    {
        // Arrange
        var deactivatedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "google@test.com",
            FullName = "Google User",
            AuthProvider = "Google",
            AuthProviderId = "google_sub_123",
            IsActive = false
        };

        var command = new GoogleLoginCommand("valid_token", "Chrome", "127.0.0.1");
        _mockGoogleAuth.Setup(g => g.ValidateIdTokenAsync("valid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_googleUser);
        _mockUow.Setup(u => u.Users.GetByAuthProviderAsync("Google", "google_sub_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deactivatedUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Error.Should().Contain("deactivated");
    }
}
