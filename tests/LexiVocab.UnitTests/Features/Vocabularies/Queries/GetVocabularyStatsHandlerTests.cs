using System.Text.Json;
using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Application.Features.Vocabularies.Queries;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Vocabularies.Queries;

public class GetVocabularyStatsHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IVocabularyRepository> _vocabRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly GetVocabularyStatsHandler _handler;

    public GetVocabularyStatsHandlerTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _vocabRepoMock = new Mock<IVocabularyRepository>();
        _uowMock.Setup(x => x.Vocabularies).Returns(_vocabRepoMock.Object);
        
        _currentUserMock = new Mock<ICurrentUserService>();
        _cacheMock = new Mock<IDistributedCache>();
        _handler = new GetVocabularyStatsHandler(_uowMock.Object, _currentUserMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCacheExists_ShouldReturnCachedData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cachedStats = new VocabularyStatsDto(100, 80, 20, 5);
        var cachedJson = JsonSerializer.Serialize(cachedStats);
        
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        
        _cacheMock.Setup(x => x.GetAsync(It.Is<string>(s => s.Contains("vocab-v")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);
            
        _cacheMock.Setup(x => x.GetAsync(It.Is<string>(s => s.Contains("vocab-stats")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _handler.Handle(new GetVocabularyStatsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().BeEquivalentTo(cachedStats);
        _vocabRepoMock.Verify(x => x.GetStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCacheDoesNotExist_ShouldFetchFromDbAndCacheIt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dbStats = (100, 80, 20, 5);
        
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        
        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);
            
        _vocabRepoMock.Setup(x => x.GetStatsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbStats);

        // Act
        var result = await _handler.Handle(new GetVocabularyStatsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Total.Should().Be(100);
        result.Data!.Active.Should().Be(80);
        result.Data!.Archived.Should().Be(20);
        result.Data!.DueToday.Should().Be(5);
        
        _cacheMock.Verify(x => x.SetAsync(
            It.Is<string>(k => k.Contains("vocab-stats")),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
