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

        return Result<List<FeatureDefinitionDto>>.Success(features.Select(f => new FeatureDefinitionDto(
            f.Id,
            f.Code,
            f.Description,
            f.ValueType,
            f.DefaultValue,
            f.CreatedAt,
            f.UpdatedAt)).ToList());
    }
}
