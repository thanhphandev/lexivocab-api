using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Vocabularies.Commands;

public record BatchImportCommand(List<CreateVocabularyCommand> Words) : IRequest<Result<int>>, IAuditedRequest, IFeatureGatedRequest
{
    public AuditAction AuditAction => AuditAction.VocabularyBulkImported;
    public string? EntityType => "UserVocabulary";
    public string FeatureCode => "BATCH_IMPORT";
    public string? QuotaLimitCode => null;
}

public class BatchImportHandler : IRequestHandler<BatchImportCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFeatureGatingService _featureGating;
    private readonly IDistributedCache _cache;
    private readonly IDateTimeProvider _dateTime;

    public BatchImportHandler(
        IUnitOfWork uow, 
        ICurrentUserService currentUser,
        IFeatureGatingService featureGating,
        IDistributedCache cache,
        IDateTimeProvider dateTime)
    {
        _uow = uow;
        _currentUser = currentUser;
        _featureGating = featureGating;
        _cache = cache;
        _dateTime = dateTime;
    }

    public async Task<Result<int>> Handle(BatchImportCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();
        
        var permissions = await _featureGating.GetPermissionsAsync(userId, ct);
        if (!permissions.HasFeature(request.FeatureCode))
        {
            return Result<int>.Forbidden("ERR_PREMIUM_REQUIRED", ErrorCode.AUTHZ_RESOURCE_FORBIDDEN);
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
                TagId = word.TagId,
                WordText = word.WordText.Trim(),
                CustomMeaning = word.CustomMeaning?.Trim(),
                ContextSentence = word.ContextSentence?.Trim(),
                SourceUrl = word.SourceUrl?.Trim(),
                MasterVocabularyId = masterVocab?.Id,
                NextReviewDate = _dateTime.UtcNow
            });
        }

        if (entities.Count > 0)
        {
            await _uow.Vocabularies.AddRangeAsync(entities, ct);
            await _uow.SaveChangesAsync(ct);

            // Update tag word counts in bulk (grouped by TagId)
            var tagCounts = entities
                .Where(e => e.TagId.HasValue)
                .GroupBy(e => e.TagId!.Value)
                .Select(g => new { TagId = g.Key, Count = g.Count() });

            foreach (var group in tagCounts)
            {
                await _uow.Tags.IncrementWordCountAsync(group.TagId, group.Count, ct);
            }

            await _cache.SetStringAsync($"vocab-v:{userId}", Guid.NewGuid().ToString(), ct);
        }
        else if (request.Words.Count > 0)
        {
            return Result<int>.Failure("All words provided already exist or are duplicates.", 400, ErrorCode.VOCAB_BATCH_IMPORT_FAILED);
        }

        return Result<int>.Created(entities.Count);
    }
}
