using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Application.Common.Mappings;
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
    private readonly IDateTimeProvider _dateTime;

    public CreateVocabularyHandler(
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

    public async Task<Result<VocabularyDto>> Handle(CreateVocabularyCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var permissions = await _featureGating.GetPermissionsAsync(userId, ct);
        var maxWords = permissions.GetLimit("MAX_WORDS");
        if (permissions.IsOverQuota("MAX_WORDS", permissions.CurrentCount))
        {
            return Result<VocabularyDto>.Forbidden("ERR_QUOTA_EXCEEDED", ErrorCode.VOCAB_QUOTA_EXCEEDED);
        }

        if (await _uow.Vocabularies.WordExistsForUserAsync(userId, request.WordText, ct))
            return Result<VocabularyDto>.Conflict($"Word '{request.WordText}' already saved.", ErrorCode.VOCAB_ALREADY_EXISTS);

        Guid? assignedTagId = request.TagId;
        if (assignedTagId == null && !string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            var url = request.SourceUrl.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var domain = uri.Host;
                if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) 
                    domain = domain[4..];
                
                var tagEntity = await _uow.Tags.GetOrCreateByDomainAsync(userId, domain, ct);
                assignedTagId = tagEntity.Id;
            }
        }

        var masterVocab = await _uow.MasterVocabularies.GetByWordAsync(request.WordText.ToLowerInvariant().Trim(), ct);

        var entity = new UserVocabulary
        {
            UserId = userId,
            TagId = assignedTagId,
            WordText = request.WordText.Trim(),
            CustomMeaning = request.CustomMeaning?.Trim(),
            ContextSentence = request.ContextSentence?.Trim(),
            SourceUrl = request.SourceUrl?.Trim(),
            NextReviewDate = _dateTime.UtcNow,
            MasterVocabularyId = masterVocab?.Id,
            MasterVocabulary = masterVocab
        };

        await _uow.Vocabularies.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        if (assignedTagId.HasValue)
        {
            await _uow.Tags.IncrementWordCountAsync(assignedTagId.Value, 1, ct);
        }

        await _cache.SetStringAsync($"vocab-v:{userId}", Guid.NewGuid().ToString(), ct);

        return Result<VocabularyDto>.Created(entity.MapToDto());
    }

}
