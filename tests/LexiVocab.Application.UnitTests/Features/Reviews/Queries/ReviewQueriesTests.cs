using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Reviews.Queries;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Reviews.Queries;

public class ReviewQueriesTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Guid _userId = Guid.NewGuid();

    public ReviewQueriesTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
    }

    [Fact]
    public async Task GetReviewSession_ShouldReturnMappedCardsAndTotalDue()
    {
        // Arrange
        var handler = new GetReviewSessionHandler(_mockUow.Object, _mockCurrentUser.Object);
        var query = new GetReviewSessionQuery(10);

        var dueVocabs = new List<UserVocabulary>
        {
            new UserVocabulary
            {
                Id = Guid.NewGuid(),
                WordText = "apple",
                CustomMeaning = "quả táo",
                RepetitionCount = 2,
                EasinessFactor = 2.4,
                MasterVocabulary = new MasterVocabulary { PhoneticUs = "/ˈæpəl/", AudioUrl = "http://example.com/apple.mp3" }
            }
        };

        _mockUow.Setup(x => x.Vocabularies.GetDueForReviewAsync(_userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dueVocabs);

        _mockUow.Setup(x => x.Vocabularies.GetStatsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((100, 50, 40, 5)); // (total, active, mastered, dueToday)

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.TotalDue.Should().Be(5);
        result.Data.Cards.Should().HaveCount(1);
        
        var card = result.Data.Cards.First();
        card.VocabularyId.Should().Be(dueVocabs[0].Id);
        card.WordText.Should().Be("apple");
        card.CustomMeaning.Should().Be("quả táo");
        card.PhoneticUs.Should().Be("/ˈæpəl/");
        card.AudioUrl.Should().Be("http://example.com/apple.mp3");
        card.RepetitionCount.Should().Be(2);
        card.EasinessFactor.Should().Be(2.4);
    }

    [Fact]
    public async Task GetReviewHistory_ShouldReturnPaginatedMappedLogs()
    {
        // Arrange
        var handler = new GetReviewHistoryHandler(_mockUow.Object, _mockCurrentUser.Object);
        var query = new GetReviewHistoryQuery(1, 10);

        var vocabId = Guid.NewGuid();
        var logs = new List<ReviewLog>
        {
            new ReviewLog
            {
                Id = Guid.NewGuid(),
                UserVocabularyId = vocabId,
                QualityScore = QualityScore.Perfect,
                TimeSpentMs = 1500,
                ReviewedAt = DateTime.UtcNow,
                UserVocabulary = new UserVocabulary { WordText = "banana" }
            }
        };

        _mockUow.Setup(x => x.ReviewLogs.GetPaginatedByUserAsync(_userId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 1)); // (items, totalCount)

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.TotalCount.Should().Be(1);
        result.Data.Items.Should().HaveCount(1);
        
        var dto = result.Data.Items.First();
        dto.UserVocabularyId.Should().Be(vocabId);
        dto.WordText.Should().Be("banana");
        dto.QualityScore.Should().Be(QualityScore.Perfect);
        dto.TimeSpentMs.Should().Be(1500);
    }
}
