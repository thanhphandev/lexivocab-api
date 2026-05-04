using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Auth.Commands;

public class ResendVerificationEmailCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IEmailQueueService> _mockEmailQueue;
    private readonly Mock<IEmailTemplateService> _mockTemplateService;
    private readonly ResendVerificationEmailHandler _handler;

    public ResendVerificationEmailCommandHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCache = new Mock<IDistributedCache>();
        _mockEmailQueue = new Mock<IEmailQueueService>();
        _mockTemplateService = new Mock<IEmailTemplateService>();

        var config = new Mock<IConfiguration>();
        config.Setup(c => c["App:Url"]).Returns("https://test.lexivocab.store");

        _mockTemplateService
            .Setup(t => t.RenderTemplateAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync("<html>Verify</html>");

        _handler = new ResendVerificationEmailHandler(
            _mockUow.Object,
            _mockCache.Object,
            _mockEmailQueue.Object,
            _mockTemplateService.Object,
            config.Object,
            new Mock<ILogger<ResendVerificationEmailHandler>>().Object);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnSuccess()
    {
        // Arrange
        _mockUow.Setup(u => u.Users.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(new ResendVerificationEmailCommand("nonexistent@test.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockEmailQueue.Verify(e => e.EnqueueEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyVerified_ShouldReturnSuccess()
    {
        // Arrange
        var user = new User { Email = "verified@test.com", EmailConfirmed = true };
        _mockUow.Setup(u => u.Users.GetByEmailAsync("verified@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new ResendVerificationEmailCommand("verified@test.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockEmailQueue.Verify(e => e.EnqueueEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUnverified_ShouldEnqueueEmail()
    {
        // Arrange
        var user = new User { Email = "unverified@test.com", EmailConfirmed = false, FullName = "Test User" };
        _mockUow.Setup(u => u.Users.GetByEmailAsync("unverified@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new ResendVerificationEmailCommand("unverified@test.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockEmailQueue.Verify(e => e.EnqueueEmail(
            "unverified@test.com",
            It.Is<string>(s => s.Contains("verification code")),
            It.IsAny<string>()), Times.Once);
        
        // Verify verification code is set
        _mockCache.Verify(c => c.SetAsync(
            It.Is<string>(k => k.StartsWith("email-verify:") && !k.Contains("cooldown")),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify cooldown is set
        _mockCache.Verify(c => c.SetAsync(
            It.Is<string>(k => k.Contains("email-verify-cooldown")),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenInCooldown_ShouldReturnFailure()
    {
        // Arrange
        var user = new User { Email = "cooldown@test.com", EmailConfirmed = false, FullName = "Test User" };
        _mockUow.Setup(u => u.Users.GetByEmailAsync("cooldown@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockCache.Setup(c => c.GetAsync("email-verify-cooldown:cooldown@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("sent"));

        // Act
        var result = await _handler.Handle(new ResendVerificationEmailCommand("cooldown@test.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(429);
        result.Error.Should().Contain("wait 2 minutes");
        _mockEmailQueue.Verify(e => e.EnqueueEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
