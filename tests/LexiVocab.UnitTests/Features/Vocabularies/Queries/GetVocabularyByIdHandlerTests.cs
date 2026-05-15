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

public class GetVocabularyByIdHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IVocabularyRepository> _vocabRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly GetVocabularyByIdHandler _handler;

    public GetVocabularyByIdHandlerTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _vocabRepoMock = new Mock<IVocabularyRepository>();
        _uowMock.Setup(x => x.Vocabularies).Returns(_vocabRepoMock.Object);
        
        _currentUserMock = new Mock<ICurrentUserService>();
        _cacheMock = new Mock<IDistributedCache>();
        _handler = new GetVocabularyByIdHandler(_uowMock.Object, _currentUserMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCacheExists_ShouldReturnCachedData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var vocabId = Guid.NewGuid();
        var cachedDto = new VocabularyDto(vocabId, null, "Hello", null, null, null, 0, 2.5, 0, DateTime.UtcNow, null, false, DateTime.UtcNow, null, null, null, null, null);
        var cachedJson = JsonSerializer.Serialize(cachedDto);
        
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        
        _cacheMock.Setup(x => x.GetAsync(It.Is<string>(s => s.Contains("vocab-v")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);
            
        _cacheMock.Setup(x => x.GetAsync(It.Is<string>(s => s.Contains("vocab-item")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _handler.Handle(new GetVocabularyByIdQuery(vocabId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Id.Should().Be(vocabId);
        result.Data.WordText.Should().Be("Hello");
        _vocabRepoMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var vocabId = Guid.NewGuid();
        
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);
            
        _vocabRepoMock.Setup(x => x.GetByIdAsync(vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as UserVocabulary);

        // Act
        var result = await _handler.Handle(new GetVocabularyByIdQuery(vocabId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenWrongUser_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var vocabId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var entity = new UserVocabulary { Id = vocabId, UserId = otherUserId, WordText = "Secret" };
        
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as byte[]);
            
        _vocabRepoMock.Setup(x => x.GetByIdAsync(vocabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _handler.Handle(new GetVocabularyByIdQuery(vocabId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}
