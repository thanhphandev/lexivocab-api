using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Tags.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Enums;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Tags;

public class TagCommandsExtendedTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IDateTimeProvider> _mockDateTime;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedNow = new(2026, 5, 15, 10, 0, 0);

    public TagCommandsExtendedTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockDateTime = new Mock<IDateTimeProvider>();
        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _mockDateTime.Setup(x => x.UtcNow).Returns(_fixedNow);
    }

    // ─── DeleteTag ──────────────────────────────────────────

    [Fact]
    public async Task DeleteTag_WhenEmpty_ShouldSucceed()
    {
        var handler = new DeleteTagHandler(_mockUow.Object, _mockCurrentUser.Object);
        var tag = new VocabTag { Id = Guid.NewGuid(), UserId = _userId, WordCount = 0 };
        _mockUow.Setup(x => x.Tags.GetByIdAsync(tag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tag);

        var result = await handler.Handle(new DeleteTagCommand(tag.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mockUow.Verify(x => x.Tags.Remove(tag), Times.Once);
    }

    [Fact]
    public async Task DeleteTag_WhenHasWords_ShouldReturnConflict()
    {
        var handler = new DeleteTagHandler(_mockUow.Object, _mockCurrentUser.Object);
        var tag = new VocabTag { Id = Guid.NewGuid(), UserId = _userId, WordCount = 5 };
        _mockUow.Setup(x => x.Tags.GetByIdAsync(tag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tag);

        var result = await handler.Handle(new DeleteTagCommand(tag.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.ErrorCode.Should().Be(ErrorCode.TAG_CANNOT_DELETE_WITH_WORDS);
    }

    [Fact]
    public async Task DeleteTag_WhenNotFound_ShouldReturn404()
    {
        var handler = new DeleteTagHandler(_mockUow.Object, _mockCurrentUser.Object);
        _mockUow.Setup(x => x.Tags.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((VocabTag?)null);

        var result = await handler.Handle(new DeleteTagCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task DeleteTag_WhenOtherUsersTag_ShouldReturn404()
    {
        var handler = new DeleteTagHandler(_mockUow.Object, _mockCurrentUser.Object);
        var otherTag = new VocabTag { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), WordCount = 0 };
        _mockUow.Setup(x => x.Tags.GetByIdAsync(otherTag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(otherTag);

        var result = await handler.Handle(new DeleteTagCommand(otherTag.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    // ─── UpdateTag ──────────────────────────────────────────

    [Fact]
    public async Task UpdateTag_WhenValid_ShouldUpdateFieldsAndTimestamp()
    {
        var handler = new UpdateTagHandler(_mockUow.Object, _mockCurrentUser.Object, _mockDateTime.Object);
        var tag = new VocabTag { Id = Guid.NewGuid(), UserId = _userId, Name = "Old" };
        _mockUow.Setup(x => x.Tags.GetByIdAsync(tag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tag);

        var result = await handler.Handle(
            new UpdateTagCommand(tag.Id, "  New Name  ", "#FF0000", "🎯", 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("New Name");
        result.Data.Slug.Should().Be("new-name");
        tag.UpdatedAt.Should().Be(_fixedNow);
    }

    [Fact]
    public async Task UpdateTag_WhenNotOwned_ShouldReturn404()
    {
        var handler = new UpdateTagHandler(_mockUow.Object, _mockCurrentUser.Object, _mockDateTime.Object);
        var otherTag = new VocabTag { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _mockUow.Setup(x => x.Tags.GetByIdAsync(otherTag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(otherTag);

        var result = await handler.Handle(
            new UpdateTagCommand(otherTag.Id, "x", null, null, 0), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}
