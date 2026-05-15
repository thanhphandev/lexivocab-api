using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Vocabularies.Commands;

public class ContributeToMasterHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly ContributeToMasterHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public ContributeToMasterHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new ContributeToMasterHandler(_mockUow.Object, _mockCurrentUser.Object);
    }

    [Fact]
    public async Task Handle_WhenAlreadyLinked_ShouldReturnTrueImmediately()
    {
        var vocab = new UserVocabulary { Id = Guid.NewGuid(), UserId = _userId, MasterVocabularyId = Guid.NewGuid() };
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(vocab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vocab);

        var result = await _handler.Handle(new ContributeToMasterCommand(vocab.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMasterExists_ShouldLinkToExisting()
    {
        var vocab = new UserVocabulary { Id = Guid.NewGuid(), UserId = _userId, WordText = "Hello" };
        var master = new MasterVocabulary { Id = Guid.NewGuid(), Word = "hello" };

        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(vocab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vocab);
        _mockUow.Setup(x => x.MasterVocabularies.GetByWordAsync("hello", It.IsAny<CancellationToken>())).ReturnsAsync(master);

        var result = await _handler.Handle(new ContributeToMasterCommand(vocab.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        vocab.MasterVocabularyId.Should().Be(master.Id);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNewWord_ShouldCreatePendingMaster()
    {
        var vocab = new UserVocabulary { Id = Guid.NewGuid(), UserId = _userId, WordText = "UniqueWord" };
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(vocab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vocab);
        _mockUow.Setup(x => x.MasterVocabularies.GetByWordAsync("uniqueword", It.IsAny<CancellationToken>())).ReturnsAsync((MasterVocabulary?)null);

        var result = await _handler.Handle(new ContributeToMasterCommand(vocab.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mockUow.Verify(x => x.MasterVocabularies.AddAsync(
            It.Is<MasterVocabulary>(m => m.Word == "uniqueword" && m.IsApproved == false && m.CreatedByUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenVocabNotFound_ShouldReturn404()
    {
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserVocabulary?)null);

        var result = await _handler.Handle(new ContributeToMasterCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}
