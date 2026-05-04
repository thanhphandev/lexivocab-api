using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Payments.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MediatR;

namespace LexiVocab.UnitTests.Features.Payments.Commands;

public class PaymentCommandsTests
{
    private readonly Mock<IPaymentServiceFactory> _mockPaymentFactory;
    private readonly Mock<IPaymentService> _mockPaymentService;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ISubscriptionRepository> _mockSubRepo;
    private readonly Mock<IPlanPricingRepository> _mockPlanRepo;
    private readonly Mock<IPaymentTransactionRepository> _mockTxRepo;
    private readonly Mock<ILogger<CreatePaymentOrderHandler>> _mockCreateLogger;
    private readonly Mock<ILogger<ProcessPaymentWebhookHandler>> _mockWebhookLogger;
    private readonly Guid _userId = Guid.NewGuid();

    public PaymentCommandsTests()
    {
        _mockPaymentFactory = new Mock<IPaymentServiceFactory>();
        _mockPaymentService = new Mock<IPaymentService>();
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockSubRepo = new Mock<ISubscriptionRepository>();
        _mockPlanRepo = new Mock<IPlanPricingRepository>();
        _mockTxRepo = new Mock<IPaymentTransactionRepository>();
        _mockCreateLogger = new Mock<ILogger<CreatePaymentOrderHandler>>();
        _mockWebhookLogger = new Mock<ILogger<ProcessPaymentWebhookHandler>>();

        _mockUow.Setup(x => x.Subscriptions).Returns(_mockSubRepo.Object);
        _mockUow.Setup(x => x.PlanPricings).Returns(_mockPlanRepo.Object);
        _mockUow.Setup(x => x.PaymentTransactions).Returns(_mockTxRepo.Object);

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _mockPaymentFactory.Setup(x => x.GetService(It.IsAny<PaymentProvider>()))
            .Returns(_mockPaymentService.Object);
    }

    [Fact]
    public async Task CreateOrder_WhenSuccessful_ShouldReturnApprovalUrl()
    {
        // Arrange
        var pricingId = Guid.NewGuid();
        _mockPlanRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new PlanPricing { Id = pricingId });
            
        var handler = new CreatePaymentOrderHandler(_mockPaymentFactory.Object, _mockCurrentUser.Object, _mockUow.Object, _mockCreateLogger.Object);
        var command = new CreatePaymentOrderCommand(pricingId.ToString(), PaymentProvider.PayPal);
        var expectedUrl = "https://www.sandbox.paypal.com/checkoutnow?token=EC-1234567890";

        _mockPaymentService.Setup(x => x.CreateOrderAsync(_userId, pricingId.ToString(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(expectedUrl);
    }

    [Fact]
    public async Task CreateOrder_WhenPaymentServiceThrows_ShouldReturnFailure()
    {
        // Arrange
        var pricingId = Guid.NewGuid();
        _mockPlanRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new PlanPricing { Id = pricingId });

        var handler = new CreatePaymentOrderHandler(_mockPaymentFactory.Object, _mockCurrentUser.Object, _mockUow.Object, _mockCreateLogger.Object);
        var command = new CreatePaymentOrderCommand(pricingId.ToString(), PaymentProvider.PayPal);

        _mockPaymentService.Setup(x => x.CreateOrderAsync(_userId, pricingId.ToString(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("PayPal API Error"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.Error.Should().Contain("Payment gateway error");
    }

    [Fact]
    public async Task CaptureOrder_WhenSuccessful_ShouldReturnSuccessMessage()
    {
        // Arrange
        var handler = new CapturePaymentOrderHandler(_mockPaymentFactory.Object, _mockCurrentUser.Object, _mockUow.Object);
        var command = new CapturePaymentOrderCommand("ORDER123");

        _mockPaymentService.Setup(x => x.CaptureOrderAsync("ORDER123", _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Contain("Payment successful");
    }

    [Fact]
    public async Task ProcessWebhook_WhenSignatureVerifies_ShouldReturnSuccess()
    {
        // Arrange
        var handler = new ProcessPaymentWebhookHandler(_mockPaymentFactory.Object, _mockWebhookLogger.Object);
        var command = new ProcessPaymentWebhookCommand(PaymentProvider.Sepay, "{}", new Dictionary<string, string>());

        _mockPaymentService.Setup(x => x.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
            .ReturnsAsync(true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockPaymentService.Verify(x => x.ProcessWebhookEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWebhook_WhenSignatureInvalid_ShouldReturnUnauthorized()
    {
        // Arrange
        var handler = new ProcessPaymentWebhookHandler(_mockPaymentFactory.Object, _mockWebhookLogger.Object);
        var command = new ProcessPaymentWebhookCommand(PaymentProvider.Sepay, "{}", new Dictionary<string, string>());

        _mockPaymentService.Setup(x => x.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
            .ReturnsAsync(false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        _mockPaymentService.Verify(x => x.ProcessWebhookEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
