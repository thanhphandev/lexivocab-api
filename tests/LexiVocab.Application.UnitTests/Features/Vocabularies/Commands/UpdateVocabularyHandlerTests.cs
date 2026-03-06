using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Vocabularies.Commands;

public class UpdateVocabularyHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly UpdateVocabularyHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _vocabId = Guid.NewGuid();

    public UpdateVocabularyHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCache = new Mock<IDistributedCache>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new UpdateVocabularyHandler(
            _mockUow.Object,
            _mockCurrentUser.Object,
            _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateVocabularyCommand(_vocabId, "new meaning", null);
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(_vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVocabulary?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenBelongsToOtherUser_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateVocabularyCommand(_vocabId, "new meaning", null);
        var existingVocab = new UserVocabulary { Id = _vocabId, UserId = Guid.NewGuid() }; // Different user
        
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(_vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingVocab);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldUpdateAndReturnSuccess()
    {
        // Arrange
        var command = new UpdateVocabularyCommand(_vocabId, "  new meaning  ", "  Context  ");
        var existingVocab = new UserVocabulary 
        { 
            Id = _vocabId, 
            UserId = _userId, 
            WordText = "word",
            CustomMeaning = "old meaning"
        };
        
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(_vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingVocab);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        
        // Verify updates
        existingVocab.CustomMeaning.Should().Be("new meaning");
        existingVocab.ContextSentence.Should().Be("Context");

        _mockUow.Verify(x => x.Vocabularies.Update(existingVocab), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k == $"vocab-v:{_userId}"),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
