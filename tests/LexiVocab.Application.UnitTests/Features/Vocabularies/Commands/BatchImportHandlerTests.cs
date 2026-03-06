using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Vocabularies.Commands;

public class BatchImportHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IFeatureGatingService> _mockFeatureGating;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly BatchImportHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public BatchImportHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockFeatureGating = new Mock<IFeatureGatingService>();
        _mockCache = new Mock<IDistributedCache>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new BatchImportHandler(
            _mockUow.Object,
            _mockCurrentUser.Object,
            _mockFeatureGating.Object,
            _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WhenNoPermission_ShouldReturnForbidden()
    {
        // Arrange
        var command = new BatchImportCommand(new List<CreateVocabularyCommand> 
        {
            new("apple", null, null, null)
        });

        _mockFeatureGating.Setup(x => x.GetPermissionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPermissionsDto("Free", 50, 0, false, false, false, null)); // CanBatchImport = false

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Error.Should().Be("ERR_PREMIUM_REQUIRED");
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldImportWordsAndReturnCount()
    {
        // Arrange
        var command = new BatchImportCommand(new List<CreateVocabularyCommand> 
        {
            new("apple", null, null, null),
            new("banana", null, null, null)
        });

        _mockFeatureGating.Setup(x => x.GetPermissionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPermissionsDto("Premium", 9999, 0, true, true, true, null)); // CanBatchImport = true

        // Mock batch duplicate check: "banana" already exists
        _mockUow.Setup(x => x.Vocabularies.GetExistingWordsAsync(_userId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "banana" });

        // Mock batch master vocab lookup: no enrichment data
        _mockUow.Setup(x => x.MasterVocabularies.GetByWordsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, MasterVocabulary>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(1); // Only "apple" was imported, "banana" skipped as duplicate

        _mockUow.Verify(x => x.Vocabularies.AddRangeAsync(It.Is<IEnumerable<UserVocabulary>>(l => l.Count() == 1), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k == $"vocab-v:{_userId}"),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
