using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Features.Coupons.Queries;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Coupons;

public class ValidateCouponHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICouponRepository> _mockCouponRepo;
    private readonly ValidateCouponHandler _handler;

    public ValidateCouponHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockCouponRepo = new Mock<ICouponRepository>();
        
        _mockUow.Setup(u => u.Coupons).Returns(_mockCouponRepo.Object);
        
        _handler = new ValidateCouponHandler(_mockUow.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCouponDoesNotExist()
    {
        // Arrange
        var request = new ValidateCouponQuery("FAKE10");
        _mockCouponRepo.Setup(r => r.GetByCodeAsync("FAKE10", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Coupon?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.ErrorCode.Should().Be(ErrorCode.PAYMENT_INVALID_COUPON);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenCouponIsInactive()
    {
        // Arrange
        var request = new ValidateCouponQuery("SUMMER50");
        var coupon = new Coupon { Code = "SUMMER50", IsActive = false };
        
        _mockCouponRepo.Setup(r => r.GetByCodeAsync("SUMMER50", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenCouponHasExpired()
    {
        // Arrange
        var request = new ValidateCouponQuery("EXPIRED");
        var coupon = new Coupon 
        { 
            Code = "EXPIRED", 
            IsActive = true,
            ValidUntil = DateTime.UtcNow.AddDays(-1)
        };
        
        _mockCouponRepo.Setup(r => r.GetByCodeAsync("EXPIRED", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.ErrorCode.Should().Be(ErrorCode.PAYMENT_COUPON_EXPIRED);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenCouponIsValid()
    {
        // Arrange
        var request = new ValidateCouponQuery("VALID50");
        var coupon = new Coupon 
        { 
            Code = "VALID50", 
            IsActive = true,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 50m
        };
        
        _mockCouponRepo.Setup(r => r.GetByCodeAsync("VALID50", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Code.Should().Be("VALID50");
        result.Data.DiscountValue.Should().Be(50m);
    }
}
