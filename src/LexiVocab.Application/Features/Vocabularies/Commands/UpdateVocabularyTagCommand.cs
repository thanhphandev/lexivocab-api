using LexiVocab.Application.Common;
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

    public UpdateVocabularyTagHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result> Handle(UpdateVocabularyTagCommand request, CancellationToken ct)
    {
        var entity = await _uow.Vocabularies.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != _currentUser.UserId)
            return Result.NotFound("Vocabulary not found.");

        if (request.TagId.HasValue)
        {
            var tag = await _uow.Tags.GetByIdAsync(request.TagId.Value, ct);
            if (tag is null || tag.UserId != _currentUser.UserId)
                return Result.NotFound("Tag not found.");
        }

        entity.TagId = request.TagId;
        entity.UpdatedAt = DateTime.UtcNow;

        _uow.Vocabularies.Update(entity);
        await _uow.SaveChangesAsync(ct);
        await _cache.SetStringAsync($"vocab-v:{_currentUser.UserId}", Guid.NewGuid().ToString(), ct);

        return Result.Success();
    }
}
