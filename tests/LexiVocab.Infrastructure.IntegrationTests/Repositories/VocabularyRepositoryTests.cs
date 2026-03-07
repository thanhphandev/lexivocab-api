using FluentAssertions;
using LexiVocab.Domain.Entities;
using LexiVocab.Infrastructure.IntegrationTests.Base;
using LexiVocab.Infrastructure.Repositories;
using Xunit;

namespace LexiVocab.Infrastructure.IntegrationTests.Repositories;

public class VocabularyRepositoryTests : BaseIntegrationTest
{


    [Fact]
    public async Task WordExistsForUserAsync_WhenWordExists_ShouldReturnTrue()
    {
        // Initialize repos inside the test where DbContext is ready
        var userRepo = new UserRepository(DbContext);
        var vocabRepo = new VocabularyRepository(DbContext);

        // Arrange: Seed User
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = LexiVocab.Domain.Enums.UserRole.User
        };
        await userRepo.AddAsync(user, CancellationToken.None);

        // Arrange: Seed Vocabulary
        var vocab = new UserVocabulary
        {
            UserId = user.Id,
            WordText = "Apple",
            NextReviewDate = DateTime.UtcNow
        };
        await vocabRepo.AddAsync(vocab, CancellationToken.None);
        
        await DbContext.SaveChangesAsync();

        // Act
        // Test ignoring case
        var existsExact = await vocabRepo.WordExistsForUserAsync(user.Id, "Apple", CancellationToken.None);
        var existsLower = await vocabRepo.WordExistsForUserAsync(user.Id, "apple", CancellationToken.None);
        var existsUpper = await vocabRepo.WordExistsForUserAsync(user.Id, "APPLE", CancellationToken.None);
        var existsNonExistent = await vocabRepo.WordExistsForUserAsync(user.Id, "Banana", CancellationToken.None);

        // Assert
        existsExact.Should().BeTrue();
        existsLower.Should().BeTrue();
        existsUpper.Should().BeTrue();
        existsNonExistent.Should().BeFalse();
    }
}
