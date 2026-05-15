using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Vocabularies.Commands;

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
    private readonly IDateTimeProvider _dateTime;

    public ArchiveVocabularyHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(ArchiveVocabularyCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var entity = await _uow.Vocabularies.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != userId)
            return Result.NotFound("Vocabulary not found.", ErrorCode.VOCAB_NOT_FOUND);

        entity.IsArchived = !entity.IsArchived; // Toggle archive status
        entity.UpdatedAt = _dateTime.UtcNow;

        _uow.Vocabularies.Update(entity);
        await _uow.SaveChangesAsync(ct);
        await _cache.SetStringAsync($"vocab-v:{userId}", Guid.NewGuid().ToString(), ct);

        return Result.Success();
    }
}
