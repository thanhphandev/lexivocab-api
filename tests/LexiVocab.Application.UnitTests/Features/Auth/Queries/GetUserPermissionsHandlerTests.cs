using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Application.Features.Auth.Queries;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Auth.Queries;

public class GetUserPermissionsHandlerTests
{
    private readonly Mock<IFeatureGatingService> _mockFeatureGating;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly GetUserPermissionsHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public GetUserPermissionsHandlerTests()
    {
        _mockFeatureGating = new Mock<IFeatureGatingService>();
        _mockCurrentUser = new Mock<ICurrentUserService>();

        _mockCurrentUser.Setup(c => c.UserId).Returns(_userId);

        _handler = new GetUserPermissionsHandler(_mockFeatureGating.Object, _mockCurrentUser.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnPermissionsFromFeatureGating()
    {
        // Arrange
        var expectedPermissions = new UserPermissionsDto(
            Plan: "Free",
            MaxVocabularies: 100,
            CurrentCount: 42,
            CanExportData: false,
            CanUseAi: false,
            CanBatchImport: false,
            PlanExpiresAt: null);

        _mockFeatureGating
            .Setup(f => f.GetPermissionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPermissions);

        // Act
        var result = await _handler.Handle(new GetUserPermissionsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().BeEquivalentTo(expectedPermissions);
    }
}
