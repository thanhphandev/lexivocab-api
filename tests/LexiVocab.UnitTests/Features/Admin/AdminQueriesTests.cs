using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Application.Features.Admin.Plans.Queries;
using LexiVocab.Application.Features.Admin.Features.Queries;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Admin;

public class AdminQueriesTests
{
    private readonly Mock<IUnitOfWork> _uowMock;

    public AdminQueriesTests()
    {
        _uowMock = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
    }

    [Fact]
    public async Task GetPlanDefinitions_ShouldReturnData()
    {
        // Arrange
        var handler = new GetPlanDefinitionsHandler(_uowMock.Object);
        var plans = new List<PlanDefinition> { 
            new PlanDefinition { Id = Guid.NewGuid(), Name = "Gold", PlanFeatures = new List<PlanFeature>(), Pricings = new List<PlanPricing>() } 
        };
        
        _uowMock.Setup(x => x.PlanDefinitions.GetAllWithFeaturesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        // Act
        var result = await handler.Handle(new GetPlanDefinitionsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data[0].Name.Should().Be("Gold");
    }

    [Fact]
    public async Task GetFeatureDefinitions_ShouldReturnData()
    {
        // Arrange
        var handler = new GetFeatureDefinitionsHandler(_uowMock.Object);
        var features = new List<FeatureDefinition> { 
            new FeatureDefinition { Id = Guid.NewGuid(), Code = "MAX_WORDS", Name = "Max Words" } 
        };
        
        _uowMock.Setup(x => x.FeatureDefinitions.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(features);

        // Act
        var result = await handler.Handle(new GetFeatureDefinitionsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data[0].Code.Should().Be("MAX_WORDS");
    }

    [Fact]
    public async Task GetPlanDefinitionById_ShouldReturnData()
    {
        // Arrange
        var id = Guid.NewGuid();
        var handler = new GetPlanDefinitionByIdHandler(_uowMock.Object);
        var plan = new PlanDefinition { Id = id, Name = "Gold", PlanFeatures = new List<PlanFeature>(), Pricings = new List<PlanPricing>() };
        
        _uowMock.Setup(x => x.PlanDefinitions.GetByIdWithFeaturesAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await handler.Handle(new GetPlanDefinitionByIdQuery(id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Name.Should().Be("Gold");
    }

    [Fact]
    public async Task GetFeatureDefinitionById_ShouldReturnData()
    {
        // Arrange
        var id = Guid.NewGuid();
        var handler = new GetFeatureDefinitionByIdHandler(_uowMock.Object);
        var feature = new FeatureDefinition { Id = id, Code = "MAX_WORDS" };
        
        _uowMock.Setup(x => x.FeatureDefinitions.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(feature);

        // Act
        var result = await handler.Handle(new GetFeatureDefinitionByIdQuery(id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Code.Should().Be("MAX_WORDS");
    }
}
