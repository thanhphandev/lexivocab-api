using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Application.Features.Payments.Queries;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Enums;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Payments;

public class PaymentsQueriesTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IFeatureGatingService> _featureGatingMock;
    private readonly Mock<IPaymentServiceFactory> _paymentFactoryMock;
    private readonly Mock<IDateTimeProvider> _mockDateTime;
    
    private readonly Guid _userId = Guid.NewGuid();

    public PaymentsQueriesTests()
    {
        _uowMock = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);
        
        _featureGatingMock = new Mock<IFeatureGatingService>();
        _paymentFactoryMock = new Mock<IPaymentServiceFactory>();
        _mockDateTime = new Mock<IDateTimeProvider>();
        _mockDateTime.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
    }

    [Fact]
    public async Task GetBillingOverview_ShouldReturnData()
    {
        // Arrange
        var handler = new GetBillingOverviewHandler(_uowMock.Object, _currentUserMock.Object, _featureGatingMock.Object);
        var permissions = new UserPermissionsDto("Pro", 100, null, new Dictionary<string, string>());
        
        _featureGatingMock.Setup(x => x.GetPermissionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);
            
        _uowMock.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _userId });
            
        _uowMock.Setup(x => x.Subscriptions.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as Subscription);

        // Act
        var result = await handler.Handle(new GetBillingOverviewQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Plan.Should().Be("Pro");
    }

    [Fact]
    public async Task GetPaymentHistory_ShouldReturnPagedData()
    {
        // Arrange
        var handler = new GetPaymentHistoryHandler(_uowMock.Object, _currentUserMock.Object, _paymentFactoryMock.Object, _mockDateTime.Object);
        var transactions = new List<PaymentTransaction> { 
            new PaymentTransaction { Id = Guid.NewGuid(), Provider = PaymentProvider.PayPal, Amount = 10, Status = PaymentStatus.Completed } 
        };
        
        _uowMock.Setup(x => x.PaymentTransactions.GetPaginatedByUserAsync(_userId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((transactions, 1));
            
        _uowMock.Setup(x => x.PaymentTransactions.GetExpiredPendingByUserAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentTransaction>());

        // Act
        var result = await handler.Handle(new GetPaymentHistoryQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Items.Should().HaveCount(1);
        result.Data.Items[0].Amount.Should().Be(10);
    }

    [Fact]
    public async Task GetSubscriptionPlans_ShouldReturnPlans()
    {
        // Arrange
        var handler = new GetSubscriptionPlansHandler(_uowMock.Object);
        var plans = new List<PlanDefinition> {
            new PlanDefinition { Id = Guid.NewGuid(), NameKey = "Free", DisplayOrder = 1, PlanFeatures = new List<PlanFeature>(), Pricings = new List<PlanPricing>() }
        };
        
        _uowMock.Setup(x => x.PlanDefinitions.GetAllWithFeaturesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        // Act
        var result = await handler.Handle(new GetSubscriptionPlansQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data[0].NameKey.Should().Be("Free");
    }
}
