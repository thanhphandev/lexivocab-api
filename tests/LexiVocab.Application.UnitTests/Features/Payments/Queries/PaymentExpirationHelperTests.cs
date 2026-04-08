using FluentAssertions;
using LexiVocab.Application.Features.Payments.Queries;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.UnitTests.Features.Payments.Queries;

public class PaymentExpirationHelperTests
{
    [Fact]
    public void ExpireIfNeeded_WhenPendingAndExpired_ShouldUpdateTransactionAndSubscription()
    {
        var now = new DateTime(2026, 3, 28, 10, 0, 0, DateTimeKind.Utc);
        var tx = new PaymentTransaction
        {
            Status = PaymentStatus.Pending,
            ExpiresAt = now.AddMinutes(-1),
            Subscription = new Subscription { Status = SubscriptionStatus.Pending }
        };

        var expired = PaymentExpirationHelper.ExpireIfNeeded(tx, now);

        expired.Should().BeTrue();
        tx.Status.Should().Be(PaymentStatus.Expired);
        tx.CancelledAt.Should().Be(now);
        tx.CancelReason.Should().Be("Expired by configured pending payment expiry.");
        tx.Subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void ExpireIfNeeded_WhenExpiryIsStillInFuture_ShouldLeaveTransactionUntouched()
    {
        var now = new DateTime(2026, 3, 28, 10, 0, 0, DateTimeKind.Utc);
        var tx = new PaymentTransaction
        {
            Status = PaymentStatus.Pending,
            ExpiresAt = now.AddMinutes(5)
        };

        var expired = PaymentExpirationHelper.ExpireIfNeeded(tx, now);

        expired.Should().BeFalse();
        tx.Status.Should().Be(PaymentStatus.Pending);
        tx.CancelledAt.Should().BeNull();
        tx.CancelReason.Should().BeNull();
    }

    [Fact]
    public void ExpireIfNeeded_WhenTransactionIsNotPending_ShouldReturnFalse()
    {
        var now = new DateTime(2026, 3, 28, 10, 0, 0, DateTimeKind.Utc);
        var tx = new PaymentTransaction
        {
            Status = PaymentStatus.Completed,
            ExpiresAt = now.AddMinutes(-5)
        };

        var expired = PaymentExpirationHelper.ExpireIfNeeded(tx, now);

        expired.Should().BeFalse();
        tx.Status.Should().Be(PaymentStatus.Completed);
        tx.CancelledAt.Should().BeNull();
    }
}
