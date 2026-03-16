using FluentAssertions;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.Infrastructure.UnitTests.Services;

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
                new() { Feature = new FeatureDefinition { Code = "MAX_WORDS" }, Value = "Unlimited" },
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
        result.GetLimit("MAX_WORDS").Should().Be(999999);
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
}
