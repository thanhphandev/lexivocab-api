using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Vocabularies.Commands;

public record UpdateVocabularyTagCommand(Guid Id, Guid? TagId) : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.VocabularyUpdated;
    public string? EntityType => "UserVocabulary";
    public string? EntityId => Id.ToString();
    public string? AdditionalInfo => $"Tag updated to {TagId?.ToString() ?? "null"}";
}

public class UpdateVocabularyTagHandler : IRequestHandler<UpdateVocabularyTagCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;
    private readonly IDateTimeProvider _dateTime;

    public UpdateVocabularyTagHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(UpdateVocabularyTagCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var entity = await _uow.Vocabularies.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != userId)
            return Result.NotFound("Vocabulary not found.", ErrorCode.VOCAB_NOT_FOUND);

        if (request.TagId.HasValue)
        {
            var tag = await _uow.Tags.GetByIdAsync(request.TagId.Value, ct);
            if (tag is null || tag.UserId != userId)
                return Result.NotFound("Tag not found.", ErrorCode.TAG_NOT_FOUND);
        }

        var oldTagId = entity.TagId;
        entity.TagId = request.TagId;
        entity.UpdatedAt = _dateTime.UtcNow;

        _uow.Vocabularies.Update(entity);
        await _uow.SaveChangesAsync(ct);

        if (oldTagId != request.TagId)
        {
            if (oldTagId.HasValue)
                await _uow.Tags.DecrementWordCountAsync(oldTagId.Value, 1, ct);
            if (request.TagId.HasValue)
                await _uow.Tags.IncrementWordCountAsync(request.TagId.Value, 1, ct);
        }

        await _cache.SetStringAsync($"vocab-v:{userId}", Guid.NewGuid().ToString(), ct);

        return Result.Success();
    }
}
