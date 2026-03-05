using FluentAssertions;
using LexiVocab.Application.Services;
using LexiVocab.Domain.Enums;
using Xunit;

namespace LexiVocab.Application.UnitTests.Services;

public class SrsAlgorithmServiceTests
{
    private readonly SrsAlgorithmService _sut;

    public SrsAlgorithmServiceTests()
    {
        _sut = new SrsAlgorithmService();
    }

    [Fact]
    public void Calculate_WithPerfectQuality_ShouldIncreaseEasinessFactorAndAdvanceInterval()
    {
        // Arrange
        var currentRepCount = 0;
        var currentEf = 2.5;
        var currentInterval = 0;
        var quality = QualityScore.Perfect; // 5

        // Act
        var result = _sut.Calculate(currentRepCount, currentEf, currentInterval, quality);

        // Assert
        result.NewRepetitionCount.Should().Be(1);
        result.NewEasinessFactor.Should().Be(2.6); // 2.5 + (0.1 - (0) * ...) = 2.6
        result.NewIntervalDays.Should().Be(1);
        result.NextReviewDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Calculate_WithCorrectWithDifficulty_ShouldDecreaseEasinessFactor()
    {
        // Arrange
        var currentRepCount = 2; // Next rep is 3
        var currentEf = 2.5;
        var currentInterval = 6;
        var quality = QualityScore.CorrectWithDifficulty; // 3

        // Act
        var result = _sut.Calculate(currentRepCount, currentEf, currentInterval, quality);

        // Assert
        result.NewRepetitionCount.Should().Be(3);
        result.NewEasinessFactor.Should().Be(2.36); // EF drops
        result.NewIntervalDays.Should().Be((int)Math.Ceiling(6 * 2.36)); // 15
    }

    [Theory]
    [InlineData(QualityScore.CompleteBlackout)]
    [InlineData(QualityScore.IncorrectButRecognized)]
    [InlineData(QualityScore.IncorrectButEasyRecall)]
    public void Calculate_WithFailingQuality_ShouldResetRepetition(QualityScore failingQuality)
    {
        // Arrange
        var currentRepCount = 5;
        var currentEf = 2.5;
        var currentInterval = 20;

        // Act
        var result = _sut.Calculate(currentRepCount, currentEf, currentInterval, failingQuality);

        // Assert
        result.NewRepetitionCount.Should().Be(0);
        result.NewIntervalDays.Should().Be(1);
        result.NewEasinessFactor.Should().BeLessThan(2.5); // EF always drops on fail
    }

    [Fact]
    public void Calculate_ShouldNeverDropBelowMinimumEasinessFactor()
    {
        // Arrange
        var currentRepCount = 10;
        var currentEf = 1.3;
        var currentInterval = 30;
        var quality = QualityScore.CompleteBlackout;

        // Act
        var result = _sut.Calculate(currentRepCount, currentEf, currentInterval, quality);

        // Assert
        result.NewEasinessFactor.Should().Be(1.3); // Minimum bound
    }
}
