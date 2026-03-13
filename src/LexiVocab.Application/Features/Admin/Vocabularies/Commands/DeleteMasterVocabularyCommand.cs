using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using LexiVocab.Domain.Entities;

namespace LexiVocab.Application.Features.Admin.Vocabularies.Commands;

public record DeleteMasterVocabularyCommand(Guid Id) : IRequest<Result<bool>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(MasterVocabulary);
    public string EntityId => Id.ToString();
}

public class DeleteMasterVocabularyHandler : IRequestHandler<DeleteMasterVocabularyCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;

    public DeleteMasterVocabularyHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(DeleteMasterVocabularyCommand request, CancellationToken ct)
    {
        var existing = await _uow.MasterVocabularies.GetByIdAsync(request.Id, ct);
        if (existing == null)
            return Result<bool>.NotFound($"Vocabulary with ID '{request.Id}' not found.");

        _uow.MasterVocabularies.Remove(existing);
        await _uow.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
