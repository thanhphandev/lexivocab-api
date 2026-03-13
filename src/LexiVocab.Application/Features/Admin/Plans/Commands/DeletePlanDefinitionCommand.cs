using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Plans.Commands;

public record DeletePlanDefinitionCommand(Guid Id) : IRequest<Result<bool>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(PlanDefinition);
    public string EntityId => Id.ToString();
}

public class DeletePlanDefinitionHandler : IRequestHandler<DeletePlanDefinitionCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;

    public DeletePlanDefinitionHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(DeletePlanDefinitionCommand request, CancellationToken ct)
    {
        var existing = await _uow.PlanDefinitions.GetByIdAsync(request.Id, ct);
        if (existing == null)
            return Result<bool>.NotFound($"Plan with ID '{request.Id}' not found.");

        _uow.PlanDefinitions.Remove(existing);
        await _uow.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
