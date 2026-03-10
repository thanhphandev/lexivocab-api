using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Payments.Commands;
using LexiVocab.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MediatR;

namespace LexiVocab.Application.UnitTests.Features.Payments.Commands;

public class PaymentCommandsTests
{
    private readonly Mock<IPaymentServiceFactory> _mockPaymentFactory;
    private readonly Mock<IPaymentService> _mockPaymentService;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<ILogger<CreatePaymentOrderHandler>> _mockCreateLogger;
    private readonly Mock<ILogger<ProcessPaymentWebhookHandler>> _mockWebhookLogger;
    private readonly Guid _userId = Guid.NewGuid();

    public PaymentCommandsTests()
    {
        _mockPaymentFactory = new Mock<IPaymentServiceFactory>();
        _mockPaymentService = new Mock<IPaymentService>();
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCreateLogger = new Mock<ILogger<CreatePaymentOrderHandler>>();
        _mockWebhookLogger = new Mock<ILogger<ProcessPaymentWebhookHandler>>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _mockPaymentFactory.Setup(x => x.GetService(It.IsAny<PaymentProvider>()))
            .Returns(_mockPaymentService.Object);
    }

    [Fact]
    public async Task CreateOrder_WhenSuccessful_ShouldReturnApprovalUrl()
    {
        // Arrange
        var handler = new CreatePaymentOrderHandler(_mockPaymentFactory.Object, _mockCurrentUser.Object, _mockCreateLogger.Object);
        var command = new CreatePaymentOrderCommand("premium_id", PaymentProvider.PayPal);
        var expectedUrl = "https://www.sandbox.paypal.com/checkoutnow?token=EC-1234567890";

        _mockPaymentService.Setup(x => x.CreateOrderAsync(_userId, "premium_id", It.IsAny<CancellationToken>()))
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
        var handler = new CreatePaymentOrderHandler(_mockPaymentFactory.Object, _mockCurrentUser.Object, _mockCreateLogger.Object);
        var command = new CreatePaymentOrderCommand("premium_id", PaymentProvider.PayPal);

        _mockPaymentService.Setup(x => x.CreateOrderAsync(_userId, "premium_id", It.IsAny<CancellationToken>()))
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
        var handler = new CapturePaymentOrderHandler(_mockPaymentFactory.Object, _mockCurrentUser.Object);
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
        var command = new ProcessPaymentWebhookCommand(PaymentProvider.Seapay, "{}", new Dictionary<string, string>());

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
        var command = new ProcessPaymentWebhookCommand(PaymentProvider.Seapay, "{}", new Dictionary<string, string>());

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
