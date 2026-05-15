using System.Text.Json;
using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Application.Features.Vocabularies.Queries;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Vocabularies.Queries;

public class GetVocabularyListHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IVocabularyRepository> _vocabRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly GetVocabularyListHandler _handler;

    public GetVocabularyListHandlerTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _vocabRepoMock = new Mock<IVocabularyRepository>();
        _uowMock.Setup(x => x.Vocabularies).Returns(_vocabRepoMock.Object);
        
        _currentUserMock = new Mock<ICurrentUserService>();
        _cacheMock = new Mock<IDistributedCache>();
        _handler = new GetVocabularyListHandler(_uowMock.Object, _currentUserMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCacheExists_ShouldReturnCachedData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cachedItems = new List<VocabularyDto> { 
            new VocabularyDto(Guid.NewGuid(), null, "Hello", "Xin chào", null, null, 0, 2.5, 0, DateTime.UtcNow, null, false, DateTime.UtcNow, null, null, null, null, null) 
        };
        var cachedResult = new PagedResult<VocabularyDto> { Items = cachedItems, TotalCount = 1, Page = 1, PageSize = 20 };
        var cachedJson = JsonSerializer.Serialize(cachedResult);
        
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        
        _cacheMock.Setup(x => x.GetAsync(It.Is<string>(s => s.Contains("vocab-v")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);
            
        _cacheMock.Setup(x => x.GetAsync(It.Is<string>(s => s.Contains("vocab-list")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _handler.Handle(new GetVocabularyListQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Items.Should().HaveCount(1);
        result.Data.Items[0].WordText.Should().Be("Hello");
        _vocabRepoMock.Verify(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCacheDoesNotExist_ShouldFetchFromDbAndCacheIt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var vocabId = Guid.NewGuid();
        var items = new List<UserVocabulary> { 
            new UserVocabulary { Id = vocabId, UserId = userId, WordText = "World" } 
        };
        
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);
            
        _vocabRepoMock.Setup(x => x.GetByUserIdAsync(userId, 1, 20, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        // Act
        var result = await _handler.Handle(new GetVocabularyListQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Items.Should().HaveCount(1);
        result.Data.Items[0].WordText.Should().Be("World");
        
        _cacheMock.Verify(x => x.SetAsync(
            It.Is<string>(k => k.Contains("vocab-list")),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
