using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Vocabularies.Commands;

public class DeleteVocabularyHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly DeleteVocabularyHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _vocabId = Guid.NewGuid();

    public DeleteVocabularyHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCache = new Mock<IDistributedCache>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new DeleteVocabularyHandler(
            _mockUow.Object,
            _mockCurrentUser.Object,
            _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldDeleteAndReturnSuccess()
    {
        // Arrange
        var command = new DeleteVocabularyCommand(_vocabId);
        var existingVocab = new UserVocabulary 
        { 
            Id = _vocabId, 
            UserId = _userId
        };
        
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(_vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingVocab);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockUow.Verify(x => x.Vocabularies.Remove(existingVocab), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k == $"vocab-v:{_userId}"),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ShouldReturn404()
    {
        // Arrange
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVocabulary?)null);

        // Act
        var result = await _handler.Handle(new DeleteVocabularyCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenOtherUsersVocab_ShouldReturn404()
    {
        // Arrange
        var otherVocab = new UserVocabulary { Id = _vocabId, UserId = Guid.NewGuid() }; // different user
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(_vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherVocab);

        // Act
        var result = await _handler.Handle(new DeleteVocabularyCommand(_vocabId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        _mockUow.Verify(x => x.Vocabularies.Remove(It.IsAny<UserVocabulary>()), Times.Never);
    }
}
