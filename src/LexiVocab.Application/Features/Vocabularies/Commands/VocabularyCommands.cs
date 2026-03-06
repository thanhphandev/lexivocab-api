using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Vocabularies.Commands;

// ─── Create Vocabulary ─────────────────────────────────────────
public record CreateVocabularyCommand(
    string WordText,
    string? CustomMeaning,
    string? ContextSentence,
    string? SourceUrl
) : IRequest<Result<VocabularyDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.VocabularyCreated;
    public string? EntityType => "UserVocabulary";
}

public class CreateVocabularyHandler : IRequestHandler<CreateVocabularyCommand, Result<VocabularyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFeatureGatingService _featureGating;
    private readonly IDistributedCache _cache;

    public CreateVocabularyHandler(
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

    public async Task<Result<VocabularyDto>> Handle(CreateVocabularyCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        if (!await _featureGating.CanCreateVocabularyAsync(userId, ct))
        {
            return Result<VocabularyDto>.Failure("ERR_QUOTA_EXCEEDED", 403);
        }

        // Duplicate check (case-insensitive)
        if (await _uow.Vocabularies.WordExistsForUserAsync(userId, request.WordText, ct))
            return Result<VocabularyDto>.Conflict($"Word '{request.WordText}' already saved.");

        // Try to link with master vocabulary for enriched data
        var masterVocab = await _uow.MasterVocabularies.GetByWordAsync(request.WordText.ToLowerInvariant().Trim(), ct);

        var entity = new UserVocabulary
        {
            UserId = userId,
            WordText = request.WordText.Trim(),
            CustomMeaning = request.CustomMeaning?.Trim(),
            ContextSentence = request.ContextSentence?.Trim(),
            SourceUrl = request.SourceUrl?.Trim(),
            MasterVocabularyId = masterVocab?.Id,
            NextReviewDate = DateTime.UtcNow // Available for review immediately
        };

        await _uow.Vocabularies.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await _cache.SetStringAsync($"vocab-v:{userId}", Guid.NewGuid().ToString(), ct);

        return Result<VocabularyDto>.Created(MapToDto(entity, masterVocab));
    }

    private static VocabularyDto MapToDto(UserVocabulary v, MasterVocabulary? m) => new(
        v.Id, v.WordText, v.CustomMeaning, v.ContextSentence, v.SourceUrl,
        v.RepetitionCount, v.EasinessFactor, v.IntervalDays,
        v.NextReviewDate, v.LastReviewedAt, v.IsArchived, v.CreatedAt,
        m?.PhoneticUk, m?.PhoneticUs, m?.AudioUrl, m?.PartOfSpeech);
}

// ─── Update Vocabulary ──────────────────────────────────────────
public record UpdateVocabularyCommand(
    Guid Id,
    string? CustomMeaning,
    string? ContextSentence
) : IRequest<Result<VocabularyDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.VocabularyUpdated;
    public string? EntityType => "UserVocabulary";
    public string? EntityId => Id.ToString();
}

public class UpdateVocabularyHandler : IRequestHandler<UpdateVocabularyCommand, Result<VocabularyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public UpdateVocabularyHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<VocabularyDto>> Handle(UpdateVocabularyCommand request, CancellationToken ct)
    {
        var entity = await _uow.Vocabularies.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != _currentUser.UserId)
            return Result<VocabularyDto>.NotFound("Vocabulary not found.");

        if (request.CustomMeaning is not null) entity.CustomMeaning = request.CustomMeaning.Trim();
        if (request.ContextSentence is not null) entity.ContextSentence = request.ContextSentence.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        _uow.Vocabularies.Update(entity);
        await _uow.SaveChangesAsync(ct);
        await _cache.SetStringAsync($"vocab-v:{_currentUser.UserId}", Guid.NewGuid().ToString(), ct);

        return Result<VocabularyDto>.Success(MapToDto(entity));
    }

    private static VocabularyDto MapToDto(UserVocabulary v) => new(
        v.Id, v.WordText, v.CustomMeaning, v.ContextSentence, v.SourceUrl,
        v.RepetitionCount, v.EasinessFactor, v.IntervalDays,
        v.NextReviewDate, v.LastReviewedAt, v.IsArchived, v.CreatedAt,
        v.MasterVocabulary?.PhoneticUk, v.MasterVocabulary?.PhoneticUs,
        v.MasterVocabulary?.AudioUrl, v.MasterVocabulary?.PartOfSpeech);
}

// ─── Archive (Soft Delete / Mark as Mastered) ───────────────────
public record ArchiveVocabularyCommand(Guid Id) : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.VocabularyUpdated;
    public string? EntityType => "UserVocabulary";
    public string? EntityId => Id.ToString();
    public string? AdditionalInfo => "Archive toggled";
}

public class ArchiveVocabularyHandler : IRequestHandler<ArchiveVocabularyCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public ArchiveVocabularyHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result> Handle(ArchiveVocabularyCommand request, CancellationToken ct)
    {
        var entity = await _uow.Vocabularies.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != _currentUser.UserId)
            return Result.NotFound("Vocabulary not found.");

        entity.IsArchived = !entity.IsArchived; // Toggle archive status
        entity.UpdatedAt = DateTime.UtcNow;

        _uow.Vocabularies.Update(entity);
        await _uow.SaveChangesAsync(ct);
        await _cache.SetStringAsync($"vocab-v:{_currentUser.UserId}", Guid.NewGuid().ToString(), ct);

        return Result.Success();
    }
}

// ─── Batch Import ───────────────────────────────────────────────
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

        // Batch lookup: 1 query instead of N queries for duplicate check
        var allWordTexts = request.Words.Select(w => w.WordText.ToLowerInvariant().Trim()).ToList();
        var existingWords = await _uow.Vocabularies.GetExistingWordsAsync(userId, allWordTexts, ct);

        // Batch lookup: 1 query instead of N queries for master vocabulary enrichment
        var newWordTexts = allWordTexts.Where(w => !existingWords.Contains(w)).ToList();
        var masterVocabMap = newWordTexts.Count > 0
            ? await _uow.MasterVocabularies.GetByWordsAsync(newWordTexts, ct)
            : new Dictionary<string, Domain.Entities.MasterVocabulary>();

        var entities = new List<UserVocabulary>();

        foreach (var word in request.Words)
        {
            var normalizedWord = word.WordText.ToLowerInvariant().Trim();

            if (existingWords.Contains(normalizedWord))
                continue; // Skip duplicates silently

            // Prevent duplicates within the same batch
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

// ─── Hard Delete (Permanent Removal) ────────────────────────────
public record DeleteVocabularyCommand(Guid Id) : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.VocabularyDeleted;
    public string? EntityType => "UserVocabulary";
    public string? EntityId => Id.ToString();
}

public class DeleteVocabularyHandler : IRequestHandler<DeleteVocabularyCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public DeleteVocabularyHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result> Handle(DeleteVocabularyCommand request, CancellationToken ct)
    {
        var entity = await _uow.Vocabularies.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != _currentUser.UserId)
            return Result.NotFound("Vocabulary not found.");

        // Hard delete — cascade removes associated ReviewLogs
        _uow.Vocabularies.Remove(entity);
        await _uow.SaveChangesAsync(ct);
        await _cache.SetStringAsync($"vocab-v:{_currentUser.UserId}", Guid.NewGuid().ToString(), ct);

        return Result.Success();
    }
}
