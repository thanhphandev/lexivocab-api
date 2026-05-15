using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Auth.Commands;

public class DeleteAccountHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly DeleteAccountHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public DeleteAccountHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockCache = new Mock<IDistributedCache>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);

        _handler = new DeleteAccountHandler(_mockUow.Object, _mockCurrentUser.Object, _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WhenUserExists_ShouldDeleteAndDeactivateToken()
    {
        // Arrange
        var user = new User { Id = _userId, Email = "test@test.com" };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new DeleteAccountCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUow.Verify(x => x.Users.Remove(user), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(x => x.SetAsync(
            It.Is<string>(k => k == $"user:deactivated:{_userId}"),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(new DeleteAccountCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}
