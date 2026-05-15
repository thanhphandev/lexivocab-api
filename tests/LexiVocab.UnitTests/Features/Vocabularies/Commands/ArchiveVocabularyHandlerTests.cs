using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Vocabularies.Commands;

public class ArchiveVocabularyHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IDateTimeProvider> _mockDateTime;
    private readonly ArchiveVocabularyHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _vocabId = Guid.NewGuid();

    public ArchiveVocabularyHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCache = new Mock<IDistributedCache>();
        _mockDateTime = new Mock<IDateTimeProvider>();
        _mockDateTime.Setup(x => x.UtcNow).Returns(new DateTime(2026, 5, 15, 10, 0, 0));

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new ArchiveVocabularyHandler(
            _mockUow.Object,
            _mockCurrentUser.Object,
            _mockCache.Object,
            _mockDateTime.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldToggleArchiveStatusAndReturnSuccess()
    {
        // Arrange
        var command = new ArchiveVocabularyCommand(_vocabId);
        var existingVocab = new UserVocabulary 
        { 
            Id = _vocabId, 
            UserId = _userId, 
            IsArchived = false
        };
        
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(_vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingVocab);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingVocab.IsArchived.Should().BeTrue();

        _mockUow.Verify(x => x.Vocabularies.Update(existingVocab), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k == $"vocab-v:{_userId}"),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyArchived_ShouldToggleOff()
    {
        // Arrange
        var existingVocab = new UserVocabulary 
        { 
            Id = _vocabId, 
            UserId = _userId, 
            IsArchived = true  // already archived
        };
        
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(_vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingVocab);

        // Act
        var result = await _handler.Handle(new ArchiveVocabularyCommand(_vocabId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingVocab.IsArchived.Should().BeFalse();  // toggled back to active
    }

    [Fact]
    public async Task Handle_WhenNotFound_ShouldReturn404()
    {
        // Arrange
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVocabulary?)null);

        // Act
        var result = await _handler.Handle(new ArchiveVocabularyCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}
