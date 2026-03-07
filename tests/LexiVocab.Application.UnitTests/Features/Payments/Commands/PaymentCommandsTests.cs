using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Payments;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Payments.Commands;

public class PaymentCommandsTests
{
    private readonly Mock<IPaymentService> _mockPaymentService;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<ILogger<CreatePaymentOrderHandler>> _mockLogger;
    private readonly Guid _userId = Guid.NewGuid();

    public PaymentCommandsTests()
    {
        _mockPaymentService = new Mock<IPaymentService>();
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockLogger = new Mock<ILogger<CreatePaymentOrderHandler>>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
    }

    [Fact]
    public async Task CreateOrder_WhenSuccessful_ShouldReturnApprovalUrl()
    {
        // Arrange
        var handler = new CreatePaymentOrderHandler(_mockPaymentService.Object, _mockCurrentUser.Object, _mockLogger.Object);
        var command = new CreatePaymentOrderCommand("premium");
        var expectedUrl = "https://www.sandbox.paypal.com/checkoutnow?token=EC-1234567890";

        _mockPaymentService.Setup(x => x.CreateOrderAsync(_userId, "premium", It.IsAny<CancellationToken>()))
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
        var handler = new CreatePaymentOrderHandler(_mockPaymentService.Object, _mockCurrentUser.Object, _mockLogger.Object);
        var command = new CreatePaymentOrderCommand("premium");

        _mockPaymentService.Setup(x => x.CreateOrderAsync(_userId, "premium", It.IsAny<CancellationToken>()))
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
        var handler = new CapturePaymentOrderHandler(_mockPaymentService.Object, _mockCurrentUser.Object);
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
    public async Task CaptureOrder_WhenFailed_ShouldReturnBadRequest()
    {
        // Arrange
        var handler = new CapturePaymentOrderHandler(_mockPaymentService.Object, _mockCurrentUser.Object);
        var command = new CapturePaymentOrderCommand("ORDER123");

        _mockPaymentService.Setup(x => x.CaptureOrderAsync("ORDER123", _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Error.Should().Contain("Failed to capture payment or order already processed.");
    }
}
