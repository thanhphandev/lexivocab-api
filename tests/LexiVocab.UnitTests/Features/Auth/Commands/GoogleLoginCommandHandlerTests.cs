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

public class GoogleLoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IAuthTokenService> _mockAuthToken;
    private readonly Mock<IGoogleAuthService> _mockGoogleAuth;
    private readonly Mock<IEmailQueueService> _mockEmailQueue;
    private readonly Mock<IEmailTemplateService> _mockTemplateService;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly GoogleLoginCommandHandler _handler;

    private readonly GoogleUserInfo _googleUser = new("google_sub_123", "google@test.com", "Google User", null);

    public GoogleLoginCommandHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockAuthToken = new Mock<IAuthTokenService>();
        _mockGoogleAuth = new Mock<IGoogleAuthService>();
        _mockEmailQueue = new Mock<IEmailQueueService>();
        _mockTemplateService = new Mock<IEmailTemplateService>();
        _mockConfig = new Mock<IConfiguration>();

        _handler = new GoogleLoginCommandHandler(
            _mockUow.Object, 
            _mockAuthToken.Object, 
            _mockGoogleAuth.Object, 
            _mockEmailQueue.Object, 
            _mockTemplateService.Object, 
            _mockConfig.Object,
            Mock.Of<ILogger<GoogleLoginCommandHandler>>());
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldCallAuthTokenService()
    {
        // Arrange
        var command = new GoogleLoginCommand("valid_token", "Chrome", "127.0.0.1");
        _mockGoogleAuth.Setup(g => g.ValidateIdTokenAsync("valid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_googleUser);
        _mockUow.Setup(u => u.Users.GetByAuthProviderAsync("Google", "google_sub_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "google@test.com", FullName = "Google User", IsActive = true });

        var authResponse = new AuthResponse(Guid.NewGuid(), "google@test.com", "Google User", "User", "access", "refresh", DateTime.UtcNow.AddDays(1), null, true, true);
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
