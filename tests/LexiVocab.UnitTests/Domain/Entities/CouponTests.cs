using FluentAssertions;
using LexiVocab.Domain.Entities;

namespace LexiVocab.UnitTests.Entities;

public class CouponTests
{
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenActiveAndUnrestricted()
    {
        // Arrange
        var coupon = new Coupon
        {
            IsActive = true
        };

        // Act
        var result = coupon.IsValid();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenInactive()
    {
        // Arrange
        var coupon = new Coupon
        {
            IsActive = false
        };

        // Act
        var result = coupon.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMaxUsesReached()
    {
        // Arrange
        var coupon = new Coupon
        {
            IsActive = true,
            MaxUses = 10,
            CurrentUses = 10
        };

        // Act
        var result = coupon.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldReturnTrue_WhenCurrentUsesLessThanMax()
    {
        // Arrange
        var coupon = new Coupon
        {
            IsActive = true,
            MaxUses = 10,
            CurrentUses = 9
        };

        // Act
        var result = coupon.IsValid();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenValidFromIsInFuture()
    {
        // Arrange
        var coupon = new Coupon
        {
            IsActive = true,
            ValidFrom = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = coupon.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenValidUntilIsInPast()
    {
        // Arrange
        var coupon = new Coupon
        {
            IsActive = true,
            ValidUntil = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var result = coupon.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldReturnTrue_WhenCurrentlyWithinDateRange()
    {
        // Arrange
        var coupon = new Coupon
        {
            IsActive = true,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidUntil = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = coupon.IsValid();

        // Assert
        result.Should().BeTrue();
    }
}
