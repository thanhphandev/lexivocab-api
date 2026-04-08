using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Tags.Commands;
using LexiVocab.Domain.Entities;

using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Tags.Commands;

public class CreateTagHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IVocabTagRepository> _mockTagRepo;
    private readonly Mock<ICurrentUserService> _mockUserService;
    private readonly CreateTagHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public CreateTagHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockTagRepo = new Mock<IVocabTagRepository>();
        _mockUserService = new Mock<ICurrentUserService>();

        _mockUserService.Setup(u => u.UserId).Returns(_userId);
        _mockUow.Setup(u => u.Tags).Returns(_mockTagRepo.Object);

        _handler = new CreateTagHandler(_mockUow.Object, _mockUserService.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenTagWithSameSlugExists()
    {
        // Arrange
        var request = new CreateTagCommand("My Tag", null, null);
        var existingTag = new VocabTag { Id = Guid.NewGuid(), Name = "My Tag", Slug = "my-tag" };

        _mockTagRepo.Setup(repo => repo.GetBySlugAsync(_userId, "my-tag", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTag);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.ErrorCode.Should().Be(ErrorCode.TAG_NAME_ALREADY_EXISTS);
        
        _mockTagRepo.Verify(r => r.AddAsync(It.IsAny<VocabTag>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreateTag_WhenValid()
    {
        // Arrange
        var request = new CreateTagCommand("New Cool Tag", "#FF0000", "🔥");

        _mockTagRepo.Setup(repo => repo.GetBySlugAsync(_userId, "new-cool-tag", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VocabTag?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("New Cool Tag");
        result.Data.Slug.Should().Be("new-cool-tag");
        result.Data.Color.Should().Be("#FF0000");
        result.Data.Icon.Should().Be("🔥");

        _mockTagRepo.Verify(r => r.AddAsync(It.Is<VocabTag>(t => t.Slug == "new-cool-tag" && t.UserId == _userId), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
