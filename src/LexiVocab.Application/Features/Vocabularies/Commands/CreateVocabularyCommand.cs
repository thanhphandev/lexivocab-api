using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Vocabularies.Commands;

public record CreateVocabularyCommand(
    string WordText,
    string? CustomMeaning,
    string? ContextSentence,
    string? SourceUrl,
    Guid? TagId = null
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

        if (await _uow.Vocabularies.WordExistsForUserAsync(userId, request.WordText, ct))
            return Result<VocabularyDto>.Conflict($"Word '{request.WordText}' already saved.");

        var masterVocab = await _uow.MasterVocabularies.GetByWordAsync(request.WordText.ToLowerInvariant().Trim(), ct);

        Guid? assignedTagId = request.TagId;
        if (!string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            try
            {
                var uri = new Uri(request.SourceUrl);
                var domain = uri.Host;
                if (domain.StartsWith("www.")) domain = domain[4..];
                
                var tagEntity = await _uow.Tags.GetOrCreateByDomainAsync(userId, domain, ct);
                assignedTagId = tagEntity.Id;
            }
            catch (UriFormatException) { }
        }

        var entity = new UserVocabulary
        {
            UserId = userId,
            TagId = assignedTagId,
            WordText = request.WordText.Trim(),
            CustomMeaning = request.CustomMeaning?.Trim(),
            ContextSentence = request.ContextSentence?.Trim(),
            SourceUrl = request.SourceUrl?.Trim(),
            MasterVocabularyId = masterVocab?.Id,
            NextReviewDate = DateTime.UtcNow
        };

        await _uow.Vocabularies.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await _cache.SetStringAsync($"vocab-v:{userId}", Guid.NewGuid().ToString(), ct);

        return Result<VocabularyDto>.Created(MapToDto(entity, masterVocab));
    }

    private static VocabularyDto MapToDto(UserVocabulary v, MasterVocabulary? m) => new(
        v.Id, v.TagId, v.WordText, v.CustomMeaning, v.ContextSentence, v.SourceUrl,
        v.RepetitionCount, v.EasinessFactor, v.IntervalDays,
        v.NextReviewDate, v.LastReviewedAt, v.IsArchived, v.CreatedAt,
        m?.PhoneticUk, m?.PhoneticUs, m?.AudioUrl, m?.PartOfSpeech);
}
