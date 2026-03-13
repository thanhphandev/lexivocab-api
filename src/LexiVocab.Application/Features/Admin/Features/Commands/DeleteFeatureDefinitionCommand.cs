using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Features.Commands;

public record DeleteFeatureDefinitionCommand(Guid Id) : IRequest<Result<bool>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(FeatureDefinition);
    public string EntityId => Id.ToString();
}

public class DeleteFeatureDefinitionHandler : IRequestHandler<DeleteFeatureDefinitionCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;

    public DeleteFeatureDefinitionHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(DeleteFeatureDefinitionCommand request, CancellationToken ct)
    {
        var existing = await _uow.FeatureDefinitions.GetByIdAsync(request.Id, ct);
        if (existing == null)
            return Result<bool>.NotFound($"Feature with ID '{request.Id}' not found.");

        _uow.FeatureDefinitions.Remove(existing);
        await _uow.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
