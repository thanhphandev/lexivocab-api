using FluentAssertions;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using Xunit;

namespace LexiVocab.UnitTests.Entities;

public class SubscriptionTests
{
    // Use a fixed reference point to avoid DateTime.UtcNow non-determinism
    private static readonly DateTime _referenceDate = new(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsExpired_ShouldReturnFalse_WhenActiveAndNoEndDate()
    {
        // Arrange: Lifetime subscription
        var subscription = new Subscription
        {
            Status = SubscriptionStatus.Active,
            EndDate = null
        };

        // Act
        var result = subscription.IsExpired();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ShouldReturnFalse_WhenActiveAndEndDateInFuture()
    {
        // Arrange
        var subscription = new Subscription
        {
            Status = SubscriptionStatus.Active,
            EndDate = DateTime.UtcNow.AddDays(30)  // safely in the future — deterministic within test execution window
        };

        // Act
        var result = subscription.IsExpired();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenEndDateInPast()
    {
        // Arrange — use a date that is always in the past regardless of when the test runs
        var subscription = new Subscription
        {
            Status = SubscriptionStatus.Active,
            EndDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var result = subscription.IsExpired();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(SubscriptionStatus.Expired)]
    [InlineData(SubscriptionStatus.Cancelled)]
    [InlineData(SubscriptionStatus.Pending)]
    public void IsExpired_ShouldReturnTrue_WhenStatusIsNotActive(SubscriptionStatus targetStatus)
    {
        // Arrange — end date doesn't matter when status is not Active
        var subscription = new Subscription
        {
            Status = targetStatus,
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        // Act
        var result = subscription.IsExpired();

        // Assert
        result.Should().BeTrue();
    }
}
