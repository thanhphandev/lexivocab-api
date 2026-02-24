using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Vocabularies.Commands;

// ─── Create Vocabulary ─────────────────────────────────────────
public record CreateVocabularyCommand(
    string WordText,
    string? CustomMeaning,
    string? ContextSentence,
    string? SourceUrl
) : IRequest<Result<VocabularyDto>>;

public class CreateVocabularyHandler : IRequestHandler<CreateVocabularyCommand, Result<VocabularyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateVocabularyHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<VocabularyDto>> Handle(CreateVocabularyCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

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
) : IRequest<Result<VocabularyDto>>;

public class UpdateVocabularyHandler : IRequestHandler<UpdateVocabularyCommand, Result<VocabularyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateVocabularyHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
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
public record ArchiveVocabularyCommand(Guid Id) : IRequest<Result>;

public class ArchiveVocabularyHandler : IRequestHandler<ArchiveVocabularyCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ArchiveVocabularyHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
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

        return Result.Success();
    }
}

// ─── Batch Import ───────────────────────────────────────────────
public record BatchImportCommand(List<CreateVocabularyCommand> Words) : IRequest<Result<int>>;

public class BatchImportHandler : IRequestHandler<BatchImportCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public BatchImportHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(BatchImportCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var entities = new List<UserVocabulary>();

        foreach (var word in request.Words)
        {
            if (await _uow.Vocabularies.WordExistsForUserAsync(userId, word.WordText, ct))
                continue; // Skip duplicates silently

            var masterVocab = await _uow.MasterVocabularies.GetByWordAsync(
                word.WordText.ToLowerInvariant().Trim(), ct);

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
        }

        return Result<int>.Created(entities.Count);
    }
}
