using FluentAssertions;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using Xunit;

namespace LexiVocab.Domain.UnitTests.Entities;

public class SubscriptionTests
{
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
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        // Act
        var result = subscription.IsExpired();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenEndDateInPast()
    {
        // Arrange
        var subscription = new Subscription
        {
            Status = SubscriptionStatus.Active,
            EndDate = DateTime.UtcNow.AddDays(-1)
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
        // Arrange
        var subscription = new Subscription
        {
            Status = targetStatus,
            EndDate = DateTime.UtcNow.AddDays(30) // End date doesn't matter if status is not Active
        };

        // Act
        var result = subscription.IsExpired();

        // Assert
        result.Should().BeTrue();
    }
}
