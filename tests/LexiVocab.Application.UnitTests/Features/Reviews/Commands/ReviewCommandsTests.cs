using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Reviews.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Reviews.Commands;

public class ReviewCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<ISrsAlgorithm> _mockSrs;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly SubmitReviewHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _vocabId = Guid.NewGuid();

    public ReviewCommandsTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockSrs = new Mock<ISrsAlgorithm>();
        _mockCache = new Mock<IDistributedCache>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new SubmitReviewHandler(
            _mockUow.Object,
            _mockCurrentUser.Object,
            _mockSrs.Object,
            _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldUpdateVocabSpacedRepetitionAndAddLog()
    {
        // Arrange
        var command = new SubmitReviewCommand(_vocabId, QualityScore.Perfect, 1200);
        var existingVocab = new UserVocabulary 
        { 
            Id = _vocabId, 
            UserId = _userId,
            RepetitionCount = 0,
            EasinessFactor = 2.5,
            IntervalDays = 0
        };
        
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(_vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingVocab);

        var sm2Result = new SrsCalculationResult(1, 2.6, 1, DateTime.UtcNow.AddDays(1));
        _mockSrs.Setup(x => x.Calculate(0, 2.5, 0, QualityScore.Perfect))
            .Returns(sm2Result);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.NewRepetitionCount.Should().Be(1);
        result.Data.NewEasinessFactor.Should().Be(2.6);
        result.Data.NewIntervalDays.Should().Be(1);

        existingVocab.RepetitionCount.Should().Be(1);
        existingVocab.EasinessFactor.Should().Be(2.6);
        existingVocab.IntervalDays.Should().Be(1);
        existingVocab.LastReviewedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));

        _mockUow.Verify(x => x.Vocabularies.Update(existingVocab), Times.Once);
        _mockUow.Verify(x => x.ReviewLogs.AddAsync(It.Is<ReviewLog>(log => 
            log.UserVocabularyId == _vocabId && 
            log.UserId == _userId &&
            log.QualityScore == QualityScore.Perfect &&
            log.TimeSpentMs == 1200), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k == $"vocab-v:{_userId}"),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenVocabNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var command = new SubmitReviewCommand(_vocabId, QualityScore.Perfect, 1200);
        _mockUow.Setup(x => x.Vocabularies.GetByIdAsync(_vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVocabulary?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}
