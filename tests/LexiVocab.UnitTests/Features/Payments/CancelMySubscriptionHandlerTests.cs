using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Payments.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Payments;

public class CancelMySubscriptionHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IDateTimeProvider> _mockDateTime;
    private readonly CancelMySubscriptionHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedNow = new(2026, 5, 15, 10, 0, 0);

    public CancelMySubscriptionHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockDateTime = new Mock<IDateTimeProvider>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _mockDateTime.Setup(x => x.UtcNow).Returns(_fixedNow);

        _handler = new CancelMySubscriptionHandler(_mockUow.Object, _mockCurrentUser.Object, _mockDateTime.Object);
    }

    [Fact]
    public async Task Handle_WhenActive_ShouldCancelAndSetEndDate()
    {
        var sub = new Subscription { Id = Guid.NewGuid(), UserId = _userId, Status = SubscriptionStatus.Active };
        _mockUow.Setup(x => x.Subscriptions.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        var result = await _handler.Handle(new CancelMySubscriptionCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.EndDate.Should().Be(_fixedNow);
        sub.UpdatedAt.Should().Be(_fixedNow);
    }

    [Fact]
    public async Task Handle_WhenAlreadyExpired_ShouldReturnError()
    {
        var expired = new Subscription { UserId = _userId, Status = SubscriptionStatus.Expired, EndDate = _fixedNow.AddDays(-5), CreatedAt = _fixedNow.AddDays(-30) };
        _mockUow.Setup(x => x.Subscriptions.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((Subscription?)null);
        _mockUow.Setup(x => x.Subscriptions.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Subscription> { expired });

        var result = await _handler.Handle(new CancelMySubscriptionCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SUB_EXPIRED);
    }

    [Fact]
    public async Task Handle_WhenAlreadyCancelled_ShouldReturnError()
    {
        var cancelled = new Subscription { UserId = _userId, Status = SubscriptionStatus.Cancelled, CreatedAt = _fixedNow.AddDays(-10) };
        _mockUow.Setup(x => x.Subscriptions.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((Subscription?)null);
        _mockUow.Setup(x => x.Subscriptions.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Subscription> { cancelled });

        var result = await _handler.Handle(new CancelMySubscriptionCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SUB_CANCELLED);
    }

    [Fact]
    public async Task Handle_WhenNoSubscriptionAtAll_ShouldReturn404()
    {
        _mockUow.Setup(x => x.Subscriptions.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((Subscription?)null);
        _mockUow.Setup(x => x.Subscriptions.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Subscription>());

        var result = await _handler.Handle(new CancelMySubscriptionCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}
