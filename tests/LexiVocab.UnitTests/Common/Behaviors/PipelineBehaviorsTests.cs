using FluentAssertions;
using FluentValidation;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Behaviors;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Common;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace LexiVocab.UnitTests.Common.Behaviors;

// ─── Test Fixtures ──────────────────────────────────────────

public sealed record TestValidatedRequest(string Email) : IRequest<Result<string>>;

public class TestValidatedRequestValidator : AbstractValidator<TestValidatedRequest>
{
    public TestValidatedRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed record TestCacheableRequest(string Key) : IRequest<Result<string>>, ICacheableQuery<Result<string>>
{
    public string CacheKey => $"test:{Key}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

// ─── ValidationBehavior Tests ───────────────────────────────

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WhenNoValidators_ShouldCallNext()
    {
        // Arrange — empty validator list
        var behavior = new ValidationBehavior<TestValidatedRequest, Result<string>>(
            Enumerable.Empty<IValidator<TestValidatedRequest>>());
        var nextCalled = false;

        // Act
        var result = await behavior.Handle(
            new TestValidatedRequest("test@test.com"),
            _ => { nextCalled = true; return Task.FromResult(Result<string>.Success("ok")); },
            CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidationPasses_ShouldCallNext()
    {
        // Arrange
        var validators = new List<IValidator<TestValidatedRequest>> { new TestValidatedRequestValidator() };
        var behavior = new ValidationBehavior<TestValidatedRequest, Result<string>>(validators);

        // Act
        var result = await behavior.Handle(
            new TestValidatedRequest("valid@email.com"),
            _ => Task.FromResult(Result<string>.Success("ok")),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ShouldThrowValidationException()
    {
        // Arrange
        var validators = new List<IValidator<TestValidatedRequest>> { new TestValidatedRequestValidator() };
        var behavior = new ValidationBehavior<TestValidatedRequest, Result<string>>(validators);

        // Act
        var act = () => behavior.Handle(
            new TestValidatedRequest(""),  // empty email — fails NotEmpty()
            _ => Task.FromResult(Result<string>.Success("should not reach")),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any());
    }

    [Fact]
    public async Task Handle_WhenMultipleValidationErrors_ShouldCollectAll()
    {
        // Arrange
        var validators = new List<IValidator<TestValidatedRequest>> { new TestValidatedRequestValidator() };
        var behavior = new ValidationBehavior<TestValidatedRequest, Result<string>>(validators);

        // Act
        var act = () => behavior.Handle(
            new TestValidatedRequest("not-an-email"),  // fails EmailAddress()
            _ => Task.FromResult(Result<string>.Success("should not reach")),
            CancellationToken.None);

        // Assert — should throw with at least the email format error
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().HaveCountGreaterThanOrEqualTo(1);
    }
}

// ─── CachingBehavior Tests ──────────────────────────────────

public class CachingBehaviorTests
{
    private readonly Mock<IDistributedCache> _mockCache = new();
    private readonly Mock<ILogger<CachingBehavior<TestCacheableRequest, Result<string>>>> _mockLogger = new();

    private CachingBehavior<TestCacheableRequest, Result<string>> CreateBehavior()
        => new(_mockCache.Object, _mockLogger.Object);

    [Fact]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedResultWithoutCallingNext()
    {
        // Arrange
        var cachedResult = Result<string>.Success("cached-data");
        var cachedBytes = JsonSerializer.SerializeToUtf8Bytes(cachedResult);
        _mockCache.Setup(x => x.GetAsync("test:key1", It.IsAny<CancellationToken>())).ReturnsAsync(cachedBytes);

        var behavior = CreateBehavior();
        var nextCalled = false;

        // Act
        var result = await behavior.Handle(
            new TestCacheableRequest("key1"),
            _ => { nextCalled = true; return Task.FromResult(Result<string>.Success("from-db")); },
            CancellationToken.None);

        // Assert
        nextCalled.Should().BeFalse(); // handler NOT called
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCacheMiss_ShouldCallNextAndCacheResult()
    {
        // Arrange
        _mockCache.Setup(x => x.GetAsync("test:key2", It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);

        var behavior = CreateBehavior();

        // Act
        var result = await behavior.Handle(
            new TestCacheableRequest("key2"),
            _ => Task.FromResult(Result<string>.Success("fresh-data")),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockCache.Verify(x => x.SetAsync(
            "test:key2",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCacheReadFails_ShouldFallbackToHandler()
    {
        // Arrange — Redis is down
        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection refused"));

        var behavior = CreateBehavior();
        var nextCalled = false;

        // Act
        var result = await behavior.Handle(
            new TestCacheableRequest("key3"),
            _ => { nextCalled = true; return Task.FromResult(Result<string>.Success("fallback")); },
            CancellationToken.None);

        // Assert — graceful degradation
        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenResultIsFailed_ShouldNotCache()
    {
        // Arrange
        _mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);

        var behavior = CreateBehavior();

        // Act — handler returns a failure result (e.g., 404)
        var result = await behavior.Handle(
            new TestCacheableRequest("key4"),
            _ => Task.FromResult(Result<string>.NotFound("not found")),
            CancellationToken.None);

        // Assert — should NOT cache error responses
        result.IsSuccess.Should().BeFalse();
        _mockCache.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
