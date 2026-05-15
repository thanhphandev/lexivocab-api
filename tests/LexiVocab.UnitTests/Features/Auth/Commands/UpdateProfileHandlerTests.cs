using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Auth.Commands;

public class UpdateProfileHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IDateTimeProvider> _mockDateTime;
    private readonly UpdateProfileHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedNow = new(2026, 5, 15, 10, 0, 0);

    public UpdateProfileHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockDateTime = new Mock<IDateTimeProvider>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _mockDateTime.Setup(x => x.UtcNow).Returns(_fixedNow);

        _handler = new UpdateProfileHandler(_mockUow.Object, _mockCurrentUser.Object, _mockDateTime.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldUpdateNameAndTimestamp()
    {
        // Arrange
        var user = new User { Id = _userId, Email = "test@test.com", FullName = "Old Name" };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var command = new UpdateProfileCommand("  New Name  ", "https://api.dicebear.com/avatar.png");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.FullName.Should().Be("New Name");    // trimmed
        result.Data.AvatarUrl.Should().Be("https://api.dicebear.com/avatar.png");
        user.UpdatedAt.Should().Be(_fixedNow);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(new UpdateProfileCommand("Name"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenNoAvatarProvided_ShouldNotOverwriteExistingAvatar()
    {
        // Arrange
        var user = new User { Id = _userId, FullName = "Test", AvatarUrl = "https://old-avatar.com/pic.png" };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new UpdateProfileCommand("Updated Name"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.AvatarUrl.Should().Be("https://old-avatar.com/pic.png"); // unchanged
    }
}
