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

namespace LexiVocab.Application.UnitTests.Features.Analytics.Queries;

public class AnalyticsQueriesTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Guid _userId = Guid.NewGuid();

    public AnalyticsQueriesTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCache = new Mock<IDistributedCache>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        // Always return null from cache to test the actual DB logic
        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
    }

    [Fact]
    public async Task GetDashboard_CacheMiss_ShouldAggregateAndReturnData()
    {
        // Arrange
        var handler = new GetDashboardHandler(_mockUow.Object, _mockCurrentUser.Object, _mockCache.Object);
        var query = new GetDashboardQuery();

        _mockUow.Setup(x => x.Vocabularies.GetStatsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((100, 80, 10, 5)); // total, active, archived, dueToday

        // Setup for reviewsToday and reviewsThisWeek
        _mockUow.Setup(x => x.ReviewLogs.GetPeriodStatsAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((20, 4.5, 3000)); // totalReviews, avgQuality, totalTimeSpent

        _mockUow.Setup(x => x.ReviewLogs.GetCurrentStreakAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var heatmapData = new List<(DateOnly Date, int Count)>
        {
            (new DateOnly(2023, 1, 1), 10),
            (new DateOnly(2023, 1, 2), 5)
        };
        _mockUow.Setup(x => x.ReviewLogs.GetHeatmapDataAsync(_userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(heatmapData);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Vocabulary.TotalWords.Should().Be(100);
        result.Data.Vocabulary.ActiveWords.Should().Be(80);
        result.Data.Vocabulary.MasteredWords.Should().Be(10);
        result.Data.Vocabulary.DueToday.Should().Be(5);
        result.Data.CurrentStreak.Should().Be(7);
        result.Data.TotalStudyDays.Should().Be(2);
        
        // Cache should be updated with new data
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k.StartsWith("analytics-dashboard:")),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHeatmap_CacheMiss_ShouldReturnHeatmapData()
    {
        // Arrange
        var handler = new GetHeatmapHandler(_mockUow.Object, _mockCurrentUser.Object, _mockCache.Object);
        var query = new GetHeatmapQuery(2023);

        var dbData = new List<(DateOnly Date, int Count)>
        {
            (new DateOnly(2023, 5, 10), 4),
            (new DateOnly(2023, 5, 11), 12)
        };
        _mockUow.Setup(x => x.ReviewLogs.GetHeatmapDataAsync(_userId, new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbData);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Year.Should().Be(2023);
        result.Data.Entries.Should().HaveCount(2);
        result.Data.Entries.First(e => e.Date == new DateOnly(2023, 5, 11)).Count.Should().Be(12);
        
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k.StartsWith("analytics-heatmap:")),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStreak_CacheMiss_ShouldReturnStreakInfo()
    {
        // Arrange
        var handler = new GetStreakHandler(_mockUow.Object, _mockCurrentUser.Object, _mockCache.Object);
        var query = new GetStreakQuery();

        _mockUow.Setup(x => x.ReviewLogs.GetCurrentStreakAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _mockUow.Setup(x => x.ReviewLogs.GetLongestStreakAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(14);

        var lastActive = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var dbData = new List<(DateOnly Date, int Count)>
        {
            (lastActive, 10)
        };
        _mockUow.Setup(x => x.ReviewLogs.GetHeatmapDataAsync(_userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbData);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.CurrentStreak.Should().Be(5);
        result.Data.LongestStreak.Should().Be(14);
        result.Data.LastActiveDate.Should().Be(lastActive);
        
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k.StartsWith("analytics-streak:")),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
