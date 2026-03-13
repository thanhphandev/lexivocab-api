using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Features.Queries;

public record GetFeatureDefinitionsQuery() : IRequest<Result<List<FeatureDefinitionDto>>>;

public class GetFeatureDefinitionsHandler : IRequestHandler<GetFeatureDefinitionsQuery, Result<List<FeatureDefinitionDto>>>
{
    private readonly IUnitOfWork _uow;

    public GetFeatureDefinitionsHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<List<FeatureDefinitionDto>>> Handle(GetFeatureDefinitionsQuery request, CancellationToken ct)
    {
        var features = await _uow.FeatureDefinitions.GetAllAsync(ct);

        var dtos = features.Select(f => new FeatureDefinitionDto(
            f.Id,
            f.Code,
            f.Name,
            f.Description,
            f.CreatedAt,
            f.UpdatedAt)).ToList();

        return Result<List<FeatureDefinitionDto>>.Success(dtos);
    }
}
