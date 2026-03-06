using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Auth.Queries;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Auth.Queries;

public class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly GetCurrentUserQueryHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public GetCurrentUserQueryHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();

        _handler = new GetCurrentUserQueryHandler(_mockUow.Object, _mockCurrentUser.Object);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        _mockCurrentUser.Setup(c => c.UserId).Returns((Guid?)null);

        // Act
        var result = await _handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Handle_WhenUserNotFoundInDb_ShouldReturnNotFound()
    {
        // Arrange
        _mockCurrentUser.Setup(c => c.UserId).Returns(_userId);
        _mockUow.Setup(u => u.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Error.Should().Contain("User not found");
    }

    [Fact]
    public async Task Handle_WhenAuthenticated_ShouldReturnUserProfile()
    {
        // Arrange
        var user = new User
        {
            Id = _userId,
            Email = "test@test.com",
            FullName = "Test User",
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastLogin = new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        _mockCurrentUser.Setup(c => c.UserId).Returns(_userId);
        _mockUow.Setup(u => u.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(_userId);
        result.Data.Email.Should().Be("test@test.com");
        result.Data.FullName.Should().Be("Test User");
        result.Data.Role.Should().Be("User");
        result.Data.IsActive.Should().BeTrue();
        result.Data.LastLogin.Should().Be(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
    }
}
