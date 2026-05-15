using System.Text.Json;
using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Analytics;
using LexiVocab.Application.Features.Analytics;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Analytics;

public class AnalyticsHandlersTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IVocabularyRepository> _vocabRepoMock;
    private readonly Mock<IReviewLogRepository> _reviewLogRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IDateTimeProvider> _dateTimeMock;
    
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc);

    public AnalyticsHandlersTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _vocabRepoMock = new Mock<IVocabularyRepository>();
        _reviewLogRepoMock = new Mock<IReviewLogRepository>();
        
        _uowMock.Setup(x => x.Vocabularies).Returns(_vocabRepoMock.Object);
        _uowMock.Setup(x => x.ReviewLogs).Returns(_reviewLogRepoMock.Object);
        
        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);
        
        _cacheMock = new Mock<IDistributedCache>();
        _dateTimeMock = new Mock<IDateTimeProvider>();
        _dateTimeMock.Setup(x => x.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task GetDashboard_ShouldReturnData_AndCacheIt()
    {
        // Arrange
        var handler = new GetDashboardHandler(_uowMock.Object, _currentUserMock.Object, _cacheMock.Object, _dateTimeMock.Object);
        
        _vocabRepoMock.Setup(x => x.GetStatsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((100, 80, 20, 10));
            
        _vocabRepoMock.Setup(x => x.GetByUserIdAsync(_userId, 1, 10, false, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserVocabulary>(), 0));
            
        _reviewLogRepoMock.Setup(x => x.GetPeriodStatsAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((5, 4.0, 0));
            
        _reviewLogRepoMock.Setup(x => x.GetCurrentStreakAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
            
        IReadOnlyList<(DateOnly Date, int Count)> heatmapData = new List<(DateOnly Date, int Count)> { (DateOnly.FromDateTime(_now), 5) };
        _reviewLogRepoMock.Setup(x => x.GetHeatmapDataAsync(_userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(heatmapData);

        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);

        // Act
        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.CurrentStreak.Should().Be(3);
        result.Data!.Vocabulary.TotalWords.Should().Be(100);
        result.Data!.TotalStudyDays.Should().Be(1);
    }

    [Fact]
    public async Task GetHeatmap_ShouldReturnData()
    {
        // Arrange
        var handler = new GetHeatmapHandler(_uowMock.Object, _currentUserMock.Object, _cacheMock.Object, _dateTimeMock.Object);
        var year = 2026;
        IReadOnlyList<(DateOnly Date, int Count)> dataPoints = new List<(DateOnly Date, int Count)> { (new DateOnly(2026, 1, 1), 10) };
        
        _reviewLogRepoMock.Setup(x => x.GetHeatmapDataAsync(_userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataPoints);
            
        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);

        // Act
        var result = await handler.Handle(new GetHeatmapQuery(year), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Year.Should().Be(year);
        result.Data!.Entries.Should().HaveCount(1);
        result.Data!.Entries[0].Count.Should().Be(10);
    }

    [Fact]
    public async Task GetStreak_ShouldReturnCurrentAndLongest()
    {
        // Arrange
        var handler = new GetStreakHandler(_uowMock.Object, _currentUserMock.Object, _cacheMock.Object, _dateTimeMock.Object);
        
        _reviewLogRepoMock.Setup(x => x.GetCurrentStreakAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _reviewLogRepoMock.Setup(x => x.GetLongestStreakAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);
            
        IReadOnlyList<(DateOnly Date, int Count)> heatmapData = new List<(DateOnly Date, int Count)> { (DateOnly.FromDateTime(_now), 1) };
        _reviewLogRepoMock.Setup(x => x.GetHeatmapDataAsync(_userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(heatmapData);

        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);

        // Act
        var result = await handler.Handle(new GetStreakQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.CurrentStreak.Should().Be(5);
        result.Data!.LongestStreak.Should().Be(15);
        result.Data!.LastActiveDate.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDueCount_ShouldReturnFromVocabStats()
    {
        // Arrange
        var handler = new GetDueCountHandler(_uowMock.Object, _currentUserMock.Object, _cacheMock.Object);
        
        _vocabRepoMock.Setup(x => x.GetStatsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((100, 80, 20, 12));
            
        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);

        // Act
        var result = await handler.Handle(new GetDueCountQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.DueCount.Should().Be(12);
    }
}
