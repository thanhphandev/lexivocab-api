using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Auth.Commands;

public class LogoutCommandHandlerTests
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _handler = new LogoutCommandHandler(_mockUow.Object, _mockCurrentUser.Object, _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenProvided_ShouldRemoveFromCache()
    {
        // Arrange
        var command = new LogoutCommand("valid_refresh_token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockCache.Verify(c => c.RemoveAsync("rf_token:valid_refresh_token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Handle_WhenRefreshTokenEmpty_ShouldSucceedWithoutCacheCall(string? emptyToken)
    {
        // Arrange
        var command = new LogoutCommand(emptyToken!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockCache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
