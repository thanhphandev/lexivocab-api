using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Vocabularies.Commands;

public class UpdateVocabularyTagHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IDateTimeProvider> _mockDateTime;
    private readonly UpdateVocabularyTagHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedNow = new(2026, 5, 15, 10, 0, 0);

    public UpdateVocabularyTagHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCache = new Mock<IDistributedCache>();
        _mockDateTime = new Mock<IDateTimeProvider>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _mockDateTime.Setup(x => x.UtcNow).Returns(_fixedNow);

        _handler = new UpdateVocabularyTagHandler(_mockUow.Object, _mockCurrentUser.Object, _mockCache.Object, _mockDateTime.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldUpdateTagAndAdjustCounts()
    {
        var oldTagId = Guid.NewGuid();
        var newTagId = Guid.NewGuid();
        var vocab = new UserVocabulary { Id = Guid.NewGuid(), UserId = _userId, TagId = oldTagId };
        var newTag = new VocabTag { Id = newTagId, UserId = _userId };

        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(vocab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vocab);
        _mockUow.Setup(x => x.Tags.GetByIdAsync(newTagId, It.IsAny<CancellationToken>())).ReturnsAsync(newTag);

        var result = await _handler.Handle(new UpdateVocabularyTagCommand(vocab.Id, newTagId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        vocab.TagId.Should().Be(newTagId);
        vocab.UpdatedAt.Should().Be(_fixedNow);
        _mockUow.Verify(x => x.Tags.DecrementWordCountAsync(oldTagId, 1, It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(x => x.Tags.IncrementWordCountAsync(newTagId, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenVocabNotFound_ShouldReturn404()
    {
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserVocabulary?)null);

        var result = await _handler.Handle(new UpdateVocabularyTagCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenTagNotOwned_ShouldReturn404()
    {
        var vocab = new UserVocabulary { Id = Guid.NewGuid(), UserId = _userId };
        var otherTag = new VocabTag { Id = Guid.NewGuid(), UserId = Guid.NewGuid() }; // different user

        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(vocab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vocab);
        _mockUow.Setup(x => x.Tags.GetByIdAsync(otherTag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(otherTag);

        var result = await _handler.Handle(new UpdateVocabularyTagCommand(vocab.Id, otherTag.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenRemovingTag_ShouldSetNull()
    {
        var oldTagId = Guid.NewGuid();
        var vocab = new UserVocabulary { Id = Guid.NewGuid(), UserId = _userId, TagId = oldTagId };
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(vocab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vocab);

        var result = await _handler.Handle(new UpdateVocabularyTagCommand(vocab.Id, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        vocab.TagId.Should().BeNull();
        _mockUow.Verify(x => x.Tags.DecrementWordCountAsync(oldTagId, 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
