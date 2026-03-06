using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Auth.Commands;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IJwtTokenService> _mockJwt;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IEmailQueueService> _mockEmailQueue;
    private readonly Mock<IEmailTemplateService> _mockTemplateService;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockJwt = new Mock<IJwtTokenService>();
        _mockHasher = new Mock<IPasswordHasher>();
        _mockCache = new Mock<IDistributedCache>();
        _mockEmailQueue = new Mock<IEmailQueueService>();
        _mockTemplateService = new Mock<IEmailTemplateService>();

        var config = new Mock<IConfiguration>();
        config.Setup(c => c["App:Url"]).Returns("https://test.lexivocab.store");

        _mockHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_value");
        _mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test_access_token");
        _mockJwt.Setup(j => j.GenerateRefreshToken()).Returns("test_refresh_token");
        _mockTemplateService
            .Setup(t => t.RenderTemplateAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync("<html>Welcome</html>");

        _handler = new RegisterCommandHandler(
            _mockUow.Object, _mockJwt.Object, _mockHasher.Object,
            _mockCache.Object, _mockEmailQueue.Object, _mockTemplateService.Object,
            config.Object);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ShouldReturnConflict()
    {
        // Arrange
        var command = new RegisterCommand("test@test.com", "Password1", "Test User", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.EmailExistsAsync("test@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("already registered");

        _mockUow.Verify(u => u.Users.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldCreateUserAndReturnCreated()
    {
        // Arrange
        var command = new RegisterCommand("  Test@Email.COM  ", "Password1", "  John Doe  ", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        User? savedUser = null;
        _mockUow.Setup(u => u.Users.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => savedUser = u);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data.Should().NotBeNull();
        result.Data!.Email.Should().Be("test@email.com");
        result.Data.FullName.Should().Be("John Doe");
        result.Data.AccessToken.Should().Be("test_access_token");
        result.Data.RefreshToken.Should().Be("test_refresh_token");

        savedUser.Should().NotBeNull();
        savedUser!.Email.Should().Be("test@email.com"); // normalized
        savedUser.FullName.Should().Be("John Doe"); // trimmed
        savedUser.PasswordHash.Should().Be("hashed_value");

        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldStoreRefreshTokenMetadataInCache()
    {
        // Arrange
        var command = new RegisterCommand("test@test.com", "Password1", "Test User", "Chrome", "192.168.1.1");
        _mockUow.Setup(u => u.Users.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCache.Verify(c => c.SetAsync(
            "rf_token:test_refresh_token",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldEnqueueWelcomeEmail()
    {
        // Arrange
        var command = new RegisterCommand("test@test.com", "Password1", "Test User", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockEmailQueue.Verify(e => e.EnqueueEmail(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("Welcome")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailTemplateFails_ShouldStillRegisterSuccessfully()
    {
        // Arrange
        var command = new RegisterCommand("test@test.com", "Password1", "Test User", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockTemplateService
            .Setup(t => t.RenderTemplateAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ThrowsAsync(new Exception("Template engine error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
    }
}
