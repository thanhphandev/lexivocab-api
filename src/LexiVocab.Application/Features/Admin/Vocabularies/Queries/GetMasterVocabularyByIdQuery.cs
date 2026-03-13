using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Vocabularies.Queries;

public record GetMasterVocabularyByIdQuery(Guid Id) : IRequest<Result<MasterVocabularyDto>>;

public class GetMasterVocabularyByIdHandler : IRequestHandler<GetMasterVocabularyByIdQuery, Result<MasterVocabularyDto>>
{
    private readonly IUnitOfWork _uow;

    public GetMasterVocabularyByIdHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<MasterVocabularyDto>> Handle(GetMasterVocabularyByIdQuery request, CancellationToken ct)
    {
        var vocab = await _uow.MasterVocabularies.GetByIdAsync(request.Id, ct);
        if (vocab == null)
            return Result<MasterVocabularyDto>.NotFound($"Vocabulary with ID '{request.Id}' not found.");

        return Result<MasterVocabularyDto>.Success(new MasterVocabularyDto(
            vocab.Id,
            vocab.Word,
            vocab.PartOfSpeech,
            vocab.PhoneticUk,
            vocab.PhoneticUs,
            vocab.AudioUrl,
            vocab.PopularityRank,
            vocab.CreatedAt,
            vocab.UpdatedAt));
    }
}
