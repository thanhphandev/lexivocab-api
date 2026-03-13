using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Features.Queries;

public record GetFeatureDefinitionByIdQuery(Guid Id) : IRequest<Result<FeatureDefinitionDto>>;

public class GetFeatureDefinitionByIdHandler : IRequestHandler<GetFeatureDefinitionByIdQuery, Result<FeatureDefinitionDto>>
{
    private readonly IUnitOfWork _uow;

    public GetFeatureDefinitionByIdHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<FeatureDefinitionDto>> Handle(GetFeatureDefinitionByIdQuery request, CancellationToken ct)
    {
        var feature = await _uow.FeatureDefinitions.GetByIdAsync(request.Id, ct);
        if (feature == null)
            return Result<FeatureDefinitionDto>.NotFound($"Feature with ID '{request.Id}' not found.");

        return Result<FeatureDefinitionDto>.Success(new FeatureDefinitionDto(
            feature.Id,
            feature.Code,
            feature.Name,
            feature.Description,
            feature.CreatedAt,
            feature.UpdatedAt));
    }
}
