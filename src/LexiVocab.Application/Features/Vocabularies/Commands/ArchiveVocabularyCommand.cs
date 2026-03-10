using LexiVocab.Application.Common;
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
