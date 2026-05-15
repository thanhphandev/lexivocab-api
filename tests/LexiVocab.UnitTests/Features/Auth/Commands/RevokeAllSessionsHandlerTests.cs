using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Auth.Commands;

public class RevokeAllSessionsHandlerTests
{
    private readonly Mock<IAuthTokenService> _mockAuthToken;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly RevokeAllSessionsHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public RevokeAllSessionsHandlerTests()
    {
        _mockAuthToken = new Mock<IAuthTokenService>();
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new RevokeAllSessionsHandler(_mockAuthToken.Object, _mockCurrentUser.Object);
    }

    [Fact]
    public async Task Handle_ShouldRevokeAllSessionsAndReturnSuccess()
    {
        // Arrange
        var command = new RevokeAllSessionsCommand("current_token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAuthToken.Verify(x => x.RevokeAllSessionsAsync(_userId, "current_token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullRefreshToken_ShouldStillSucceed()
    {
        // Act
        var result = await _handler.Handle(new RevokeAllSessionsCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAuthToken.Verify(x => x.RevokeAllSessionsAsync(_userId, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
