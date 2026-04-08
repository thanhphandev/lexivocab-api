using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.AI;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.AI.Handlers;

public class TranslateHandlerTests
{
    private readonly Mock<ITranslationStreamService> _mockTranslationService;
    private readonly Mock<IFeatureGatingService> _mockFeatureGating;
    private readonly Mock<ICurrentUserService> _mockUserService;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly TranslateHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly User _testUser;

    public TranslateHandlerTests()
    {
        _mockTranslationService = new Mock<ITranslationStreamService>();
        _mockFeatureGating = new Mock<IFeatureGatingService>();
        _mockUserService = new Mock<ICurrentUserService>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockUserRepo = new Mock<IUserRepository>();

        _testUser = new User { Id = _userId };

        _mockUserService.Setup(u => u.UserId).Returns(_userId);
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        _mockUserRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testUser);

        _handler = new TranslateHandler(
            _mockTranslationService.Object,
            _mockFeatureGating.Object,
            _mockUserService.Object,
            _mockUow.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnQuotaError_WhenQuotaExceeded()
    {
        // Arrange
        var request = new TranslateQuery("apple");
        _mockFeatureGating.Setup(g => g.GetPermissionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LexiVocab.Application.DTOs.Auth.UserPermissionsDto());
        _mockFeatureGating.Setup(g => g.ConsumeQuotaAsync(_userId, "AI_ACCESS", "LLM_TRANSLATION_LIMIT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorCode.Should().Be(ErrorCode.AI_QUOTA_EXCEEDED);
        
        _mockTranslationService.Verify(s => s.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnTranslatedString_WhenSuccessful()
    {
        // Arrange
        var request = new TranslateQuery("apple");
        _mockFeatureGating.Setup(g => g.GetPermissionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LexiVocab.Application.DTOs.Auth.UserPermissionsDto());
        _mockFeatureGating.Setup(g => g.ConsumeQuotaAsync(_userId, "AI_ACCESS", "LLM_TRANSLATION_LIMIT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockTranslationService.Setup(s => s.TranslateAsync("apple", null, null, null, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"translation\": \"táo\"}");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Contain("táo");
    }

    [Fact]
    public async Task Handle_ShouldReturnAiProviderError_WhenServiceThrowsHttpRequestException()
    {
        // Arrange
        var request = new TranslateQuery("apple");
        _mockFeatureGating.Setup(g => g.GetPermissionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LexiVocab.Application.DTOs.Auth.UserPermissionsDto());
        _mockFeatureGating.Setup(g => g.ConsumeQuotaAsync(_userId, "AI_ACCESS", "LLM_TRANSLATION_LIMIT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockTranslationService.Setup(s => s.TranslateAsync("apple", null, null, null, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("Network error"));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.ErrorCode.Should().Be(ErrorCode.AI_SERVICE_UNAVAILABLE);
    }
}
