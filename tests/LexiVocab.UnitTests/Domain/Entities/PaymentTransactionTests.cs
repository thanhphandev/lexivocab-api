using FluentAssertions;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;

namespace LexiVocab.UnitTests.Entities;

public class PaymentTransactionTests
{
    [Theory]
    [InlineData(PaymentStatus.Completed)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.Expired)]
    [InlineData(PaymentStatus.Cancelled)]
    public void IsTerminal_ShouldBeTrueForTerminalStatuses(PaymentStatus status)
    {
        var tx = new PaymentTransaction { Status = status };

        tx.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_ShouldBeFalseForPendingStatus()
    {
        var tx = new PaymentTransaction { Status = PaymentStatus.Pending };

        tx.IsTerminal.Should().BeFalse();
    }
}
