using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using LexiVocab.Application.DTOs.Vocabulary;

namespace LexiVocab.Application.UnitTests.Features.Vocabularies.Commands;

public class CreateVocabularyHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IFeatureGatingService> _mockFeatureGating;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly CreateVocabularyHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public CreateVocabularyHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockFeatureGating = new Mock<IFeatureGatingService>();
        _mockCache = new Mock<IDistributedCache>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new CreateVocabularyHandler(
            _mockUow.Object,
            _mockCurrentUser.Object,
            _mockFeatureGating.Object,
            _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WhenQuotaExceeded_ShouldReturnForbiddenResult()
    {
        // Arrange
        var command = new CreateVocabularyCommand("hello", "xin chao", null, null);
        
        _mockFeatureGating
            .Setup(x => x.CanCreateVocabularyAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Quota exceeded

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Error.Should().Be("ERR_QUOTA_EXCEEDED");
        
        // Verify DB was NOT called
        _mockUow.Verify(x => x.Vocabularies.AddAsync(It.IsAny<UserVocabulary>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenWordAlreadyExists_ShouldReturnConflictResult()
    {
        // Arrange
        var command = new CreateVocabularyCommand("hello", "xin chao", null, null);
        
        _mockFeatureGating
            .Setup(x => x.CanCreateVocabularyAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockUow.Setup(x => x.Vocabularies.WordExistsForUserAsync(_userId, "hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Word exists

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409); // Conflict
        result.Error.Should().Contain("already saved");

        // Verify word was not added
        _mockUow.Verify(x => x.Vocabularies.AddAsync(It.IsAny<UserVocabulary>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldSaveVocabularyAndReturnCreatedResult()
    {
        // Arrange
        var command = new CreateVocabularyCommand("  Ubiquitous  ", "có mặt khắp nơi", null, null);
        
        _mockFeatureGating
            .Setup(x => x.CanCreateVocabularyAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockUow.Setup(x => x.Vocabularies.WordExistsForUserAsync(_userId, "  Ubiquitous  ", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Mock master dictionary lookup
        var masterVocab = new MasterVocabulary { Id = Guid.NewGuid(), Word = "ubiquitous", PhoneticUs = "/yoobik/" };
        _mockUow.Setup(x => x.MasterVocabularies.GetByWordAsync("ubiquitous", It.IsAny<CancellationToken>()))
            .ReturnsAsync(masterVocab);

        // Capture saved entity
        UserVocabulary? savedEntity = null;
        _mockUow.Setup(x => x.Vocabularies.AddAsync(It.IsAny<UserVocabulary>(), It.IsAny<CancellationToken>()))
            .Callback<UserVocabulary, CancellationToken>((v, _) => savedEntity = v);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201); // Created
        
        // Verify entity fields are trimmed correctly
        savedEntity.Should().NotBeNull();
        savedEntity!.UserId.Should().Be(_userId);
        savedEntity.WordText.Should().Be("Ubiquitous");
        savedEntity.CustomMeaning.Should().Be("có mặt khắp nơi");
        savedEntity.MasterVocabularyId.Should().Be(masterVocab.Id);
        
        // Ensure cache invalidation was called
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k == $"vocab-v:{_userId}"),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
            
        // Ensure changes were saved
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
