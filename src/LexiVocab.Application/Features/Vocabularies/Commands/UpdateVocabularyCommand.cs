using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Common.Mappings;
using LexiVocab.Application.DTOs.Vocabulary;
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
    private readonly IDateTimeProvider _dateTime;

    public UpdateVocabularyHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
        _dateTime = dateTime;
    }

    public async Task<Result<VocabularyDto>> Handle(UpdateVocabularyCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var entity = await _uow.Vocabularies.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != userId)
            return Result<VocabularyDto>.NotFound("Vocabulary not found.", ErrorCode.VOCAB_NOT_FOUND);

        if (request.CustomMeaning is not null) entity.CustomMeaning = request.CustomMeaning.Trim();
        if (request.ContextSentence is not null) entity.ContextSentence = request.ContextSentence.Trim();
        entity.UpdatedAt = _dateTime.UtcNow;

        _uow.Vocabularies.Update(entity);
        await _uow.SaveChangesAsync(ct);
        await _cache.SetStringAsync($"vocab-v:{userId}", Guid.NewGuid().ToString(), ct);

        return Result<VocabularyDto>.Success(entity.MapToDto());
    }
}
