using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Auth.Commands;

public class LogoutCommandHandlerTests
{
    private readonly Mock<IAuthTokenService> _mockAuthToken;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _mockAuthToken = new Mock<IAuthTokenService>();
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _handler = new LogoutCommandHandler(_mockAuthToken.Object, _mockCurrentUser.Object);
    }

    [Fact]
    public async Task Handle_ShouldCallRevokeRefreshTokenAsync()
    {
        // Arrange
        var command = new LogoutCommand("refresh_token");
        var userId = Guid.NewGuid();
        _mockCurrentUser.Setup(c => c.UserId).Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAuthToken.Verify(a => a.RevokeRefreshTokenAsync("refresh_token", userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
