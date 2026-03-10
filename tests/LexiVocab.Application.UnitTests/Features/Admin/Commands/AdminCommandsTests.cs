using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Features.Admin.Commands;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.Application.UnitTests.Features.Admin.Commands;

public class AdminCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Guid _userId = Guid.NewGuid();

    public AdminCommandsTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
    }

    [Fact]
    public async Task AddManualSubscription_WhenValid_ShouldCreateActiveSubscription()
    {
        // Arrange
        var handler = new AddManualSubscriptionHandler(_mockUow.Object);
        var command = new AddManualSubscriptionCommand(_userId, "Premium", 30);
        
        var user = new User { Id = _userId, FullName = "Test User" };
        var plan = new PlanDefinition { Id = Guid.NewGuid(), Name = "Premium" };

        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUow.Setup(x => x.PlanDefinitions.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _mockUow.Setup(x => x.Subscriptions.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUow.Verify(x => x.Subscriptions.AddAsync(It.Is<Subscription>(s => 
            s.UserId == _userId && 
            s.PlanDefinitionId == plan.Id && 
            s.Status == SubscriptionStatus.Active), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddManualSubscription_WhenPlanNotFound_ShouldReturnFailure()
    {
        // Arrange
        var handler = new AddManualSubscriptionHandler(_mockUow.Object);
        var command = new AddManualSubscriptionCommand(_userId, "NonExistent", 30);

        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _userId });
        _mockUow.Setup(x => x.PlanDefinitions.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlanDefinition?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}
