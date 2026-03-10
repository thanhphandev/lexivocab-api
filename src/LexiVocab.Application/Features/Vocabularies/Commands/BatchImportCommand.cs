using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Vocabularies.Commands;

public record BatchImportCommand(List<CreateVocabularyCommand> Words) : IRequest<Result<int>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.VocabularyBulkImported;
    public string? EntityType => "UserVocabulary";
}

public class BatchImportHandler : IRequestHandler<BatchImportCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFeatureGatingService _featureGating;
    private readonly IDistributedCache _cache;

    public BatchImportHandler(
        IUnitOfWork uow, 
        ICurrentUserService currentUser,
        IFeatureGatingService featureGating,
        IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _featureGating = featureGating;
        _cache = cache;
    }

    public async Task<Result<int>> Handle(BatchImportCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        
        var permissions = await _featureGating.GetPermissionsAsync(userId, ct);
        if (!permissions.CanBatchImport)
        {
            return Result<int>.Failure("ERR_PREMIUM_REQUIRED", 403);
        }

        var allWordTexts = request.Words.Select(w => w.WordText.ToLowerInvariant().Trim()).ToList();
        var existingWords = await _uow.Vocabularies.GetExistingWordsAsync(userId, allWordTexts, ct);

        var newWordTexts = allWordTexts.Where(w => !existingWords.Contains(w)).ToList();
        var masterVocabMap = newWordTexts.Count > 0
            ? await _uow.MasterVocabularies.GetByWordsAsync(newWordTexts, ct)
            : new Dictionary<string, Domain.Entities.MasterVocabulary>();

        var entities = new List<UserVocabulary>();

        foreach (var word in request.Words)
        {
            var normalizedWord = word.WordText.ToLowerInvariant().Trim();

            if (existingWords.Contains(normalizedWord))
                continue;

            if (entities.Any(e => e.WordText.Equals(word.WordText.Trim(), StringComparison.OrdinalIgnoreCase)))
                continue;

            masterVocabMap.TryGetValue(normalizedWord, out var masterVocab);

            entities.Add(new UserVocabulary
            {
                UserId = userId,
                WordText = word.WordText.Trim(),
                CustomMeaning = word.CustomMeaning?.Trim(),
                ContextSentence = word.ContextSentence?.Trim(),
                SourceUrl = word.SourceUrl?.Trim(),
                MasterVocabularyId = masterVocab?.Id,
                NextReviewDate = DateTime.UtcNow
            });
        }

        if (entities.Count > 0)
        {
            await _uow.Vocabularies.AddRangeAsync(entities, ct);
            await _uow.SaveChangesAsync(ct);
            await _cache.SetStringAsync($"vocab-v:{userId}", Guid.NewGuid().ToString(), ct);
        }

        return Result<int>.Created(entities.Count);
    }
}
