using FluentAssertions;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace LexiVocab.UnitTests.Services;

public class FeatureGatingServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly FeatureGatingService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public FeatureGatingServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockCache = new Mock<IDistributedCache>();
        _service = new FeatureGatingService(_mockUow.Object, _mockCache.Object);
    }

    [Fact]
    public async Task GetPermissions_WithActiveSub_ShouldReturnPlanDetails()
    {
        // Arrange
        var plan = new PlanDefinition 
        { 
            Id = Guid.NewGuid(), 
            Name = "Premium",
            PlanFeatures = new List<PlanFeature>
            {
                new() { Feature = new FeatureDefinition { Code = "MAX_WORDS" }, Value = "-1" },
                new() { Feature = new FeatureDefinition { Code = "AI_ACCESS" }, Value = "true" },
                new() { Feature = new FeatureDefinition { Code = "EXPORT_PDF" }, Value = "true" },
                new() { Feature = new FeatureDefinition { Code = "BATCH_IMPORT" }, Value = "true" }
            }
        };
        var sub = new Subscription 
        { 
            UserId = _userId, 
            Status = SubscriptionStatus.Active, 
            PlanDefinition = plan,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _userId });
        _mockUow.Setup(x => x.Vocabularies.CountByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _mockUow.Setup(x => x.Subscriptions.GetActiveWithFeaturesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sub);

        // Act
        var result = await _service.GetPermissionsAsync(_userId, CancellationToken.None);

        // Assert
        result.Plan.Should().Be("Premium");
        result.HasFeature("AI_ACCESS").Should().BeTrue();
        result.GetLimit("MAX_WORDS").Should().Be(-1);
        result.CurrentCount.Should().Be(10);
    }

    [Fact]
    public async Task GetPermissions_WithNoActiveSub_ShouldFallbackToFree()
    {
        // Arrange
        var freePlan = new PlanDefinition 
        { 
            Name = "Free",
            PlanFeatures = new List<PlanFeature>
            {
                new() { Feature = new FeatureDefinition { Code = "MAX_WORDS" }, Value = "50" },
                new() { Feature = new FeatureDefinition { Code = "AI_ACCESS" }, Value = "false" }
            }
        };

        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _userId });
        _mockUow.Setup(x => x.Vocabularies.CountByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _mockUow.Setup(x => x.Subscriptions.GetActiveWithFeaturesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);
        _mockUow.Setup(x => x.PlanDefinitions.GetByNameWithFeaturesAsync("Free", It.IsAny<CancellationToken>()))
            .ReturnsAsync(freePlan);

        // Act
        var result = await _service.GetPermissionsAsync(_userId, CancellationToken.None);

        // Assert
        result.Plan.Should().Be("Free");
        result.GetLimit("MAX_WORDS").Should().Be(50);
        result.HasFeature("AI_ACCESS").Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeQuota_WithRedisAtomicScript_ConcurrentRequests_ShouldRespectLimit()
    {
        // Arrange
        const int limit = 5;
        const int totalRequests = 40;
        var plan = new PlanDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Premium",
            PlanFeatures = new List<PlanFeature>
            {
                new() { Feature = new FeatureDefinition { Code = "ADVANCED_AI" }, Value = "true" },
                new() { Feature = new FeatureDefinition { Code = "AI_DAILY_LIMIT" }, Value = limit.ToString() }
            }
        };
        var activeSub = new Subscription
        {
            UserId = _userId,
            Status = SubscriptionStatus.Active,
            EndDate = DateTime.UtcNow.AddDays(30),
            PlanDefinition = plan
        };

        _mockUow.Setup(x => x.Vocabularies.CountByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockUow.Setup(x => x.Subscriptions.GetActiveWithFeaturesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSub);

        var mockDb = new Mock<IDatabase>();
        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);

        // Mock StringGetAsync for the 3 quota keys to avoid IndexOutOfRangeException
        mockDb.Setup(x => x.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { RedisValue.Null, RedisValue.Null, RedisValue.Null });

        var current = 0;
        mockDb.Setup(x => x.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Returns((string _, RedisKey[] _, RedisValue[] values, CommandFlags _) =>
            {
                var quotaLimit = int.Parse(values[0].ToString());
                var next = Interlocked.Increment(ref current);

                if (next <= quotaLimit)
                {
                    return Task.FromResult(RedisResult.Create(next));
                }

                Interlocked.Decrement(ref current);
                return Task.FromResult(RedisResult.Create(-1));
            });

        var service = new FeatureGatingService(_mockUow.Object, _mockCache.Object, mockRedis.Object);

        // Act
        var tasks = Enumerable.Range(0, totalRequests)
            .Select(_ => service.ConsumeQuotaAsync(_userId, "ADVANCED_AI", "AI_DAILY_LIMIT", CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Count(x => x).Should().Be(limit);
        results.Count(x => !x).Should().Be(totalRequests - limit);
        mockDb.Verify(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Exactly(totalRequests));
    }
}
