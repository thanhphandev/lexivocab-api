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

public class RegisterCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IAuthTokenService> _mockAuthToken;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IEmailQueueService> _mockEmailQueue;
    private readonly Mock<IEmailTemplateService> _mockTemplateService;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
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
    public async Task Handle_WhenValid_ShouldCallAuthTokenService()
    {
        // Arrange
        var command = new RegisterCommand("new@test.com", "Password123!", "New User", "Chrome", "127.0.0.1");
        _mockUow.Setup(u => u.Users.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        User? savedUser = null;
        _mockUow.Setup(u => u.Users.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => savedUser = u);

        var authResponse = new AuthResponse(Guid.NewGuid(), "new@test.com", "New User", "User", "access", "refresh", DateTime.UtcNow.AddDays(1), null, true, true);
        _mockAuthToken.Setup(a => a.IssueTokenPairAsync(It.IsAny<User>(), command.DeviceInfo, command.IpAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(authResponse);
        _mockAuthToken.Verify(a => a.IssueTokenPairAsync(It.IsAny<User>(), command.DeviceInfo, command.IpAddress, It.IsAny<CancellationToken>()), Times.Once);
    }
}
