using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Vocabularies.Commands;

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
        v.Id, v.TagId, v.WordText, v.CustomMeaning, v.ContextSentence, v.SourceUrl,
        v.RepetitionCount, v.EasinessFactor, v.IntervalDays,
        v.NextReviewDate, v.LastReviewedAt, v.IsArchived, v.CreatedAt,
        v.MasterVocabulary?.PhoneticUk, v.MasterVocabulary?.PhoneticUs,
        v.MasterVocabulary?.AudioUrl, v.MasterVocabulary?.PartOfSpeech);
}
