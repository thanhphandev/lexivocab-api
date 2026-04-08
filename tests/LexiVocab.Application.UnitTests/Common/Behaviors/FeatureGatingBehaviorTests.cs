using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Behaviors;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace LexiVocab.Application.UnitTests.Common.Behaviors;

public sealed record TestFeatureRequest(string FeatureCode, string? QuotaLimitCode = null) : IFeatureGatedRequest;

public class FeatureGatingBehaviorTests
{
    private readonly Mock<IFeatureGatingService> _featureGating = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<ILogger<FeatureGatingBehavior<TestFeatureRequest, Result<string>>>> _logger = new();

    [Fact]
    public async Task Handle_WhenUserIsUnauthenticated_ShouldThrow()
    {
        _currentUser.SetupGet(x => x.UserId).Returns((Guid?)null);
        var behavior = CreateBehavior();

        var act = () => behavior.Handle(new TestFeatureRequest("AI_ACCESS"), _ => Task.FromResult(Result<string>.Success("ok")), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Authentication required.");
    }

    [Fact]
    public async Task Handle_WhenFeatureIsMissing_ShouldReturnForbiddenResult()
    {
        var userId = Guid.NewGuid();
        _currentUser.SetupGet(x => x.UserId).Returns(userId);
        _featureGating.Setup(x => x.HasFeatureAsync(userId, "AI_ACCESS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var behavior = CreateBehavior();

        var result = await behavior.Handle(new TestFeatureRequest("AI_ACCESS"), _ => Task.FromResult(Result<string>.Success("ok")), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorCode.Should().Be(ErrorCode.AUTHZ_INSUFFICIENT_PERMISSIONS);
    }

    [Fact]
    public async Task Handle_WhenAiQuotaIsExceeded_ShouldReturnAiQuotaError()
    {
        var userId = Guid.NewGuid();
        _currentUser.SetupGet(x => x.UserId).Returns(userId);
        _featureGating.Setup(x => x.ConsumeQuotaAsync(userId, "AI_ACCESS", "AI_DAILY_LIMIT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var behavior = CreateBehavior();

        var result = await behavior.Handle(
            new TestFeatureRequest("AI_ACCESS", "AI_DAILY_LIMIT"),
            _ => Task.FromResult(Result<string>.Success("ok")),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorCode.Should().Be(ErrorCode.AI_QUOTA_EXCEEDED);
    }

    [Fact]
    public async Task Handle_WhenFeatureCheckPasses_ShouldCallNext()
    {
        var userId = Guid.NewGuid();
        var nextCalled = false;
        _currentUser.SetupGet(x => x.UserId).Returns(userId);
        _featureGating.Setup(x => x.HasFeatureAsync(userId, "EXPORT_PDF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var behavior = CreateBehavior();

        var result = await behavior.Handle(
            new TestFeatureRequest("EXPORT_PDF"),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(Result<string>.Success("passed"));
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("passed");
    }

    private FeatureGatingBehavior<TestFeatureRequest, Result<string>> CreateBehavior()
        => new(_featureGating.Object, _currentUser.Object, _logger.Object);
}
