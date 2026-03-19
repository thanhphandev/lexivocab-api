using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Vocabularies.Queries;

public record SuggestMasterVocabFromDictionaryQuery(string Word) : IRequest<Result<MasterVocabularyDto>>;

public class SuggestMasterVocabFromDictionaryHandler : IRequestHandler<SuggestMasterVocabFromDictionaryQuery, Result<MasterVocabularyDto>>
{
    private readonly IDictionaryService _dictService;

    public SuggestMasterVocabFromDictionaryHandler(IDictionaryService dictService)
    {
        _dictService = dictService;
    }

    public async Task<Result<MasterVocabularyDto>> Handle(SuggestMasterVocabFromDictionaryQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Word))
            return Result<MasterVocabularyDto>.Failure("Word parameter is required.");

        var dictionaryResult = await _dictService.FetchWordDefinitionAsync(request.Word.Trim().ToLowerInvariant(), ct);
        
        if (dictionaryResult == null)
        {
            return Result<MasterVocabularyDto>.NotFound($"No dictionary definition found for '{request.Word}'.");
        }

        // Map the Domain entity to DTO to send back to the frontend form
        var dto = new MasterVocabularyDto(
            Guid.Empty,
            dictionaryResult.Word,
            dictionaryResult.PartOfSpeech,
            dictionaryResult.PhoneticUk,
            dictionaryResult.PhoneticUs,
            dictionaryResult.AudioUrl,
            null, // PopularityRank
            dictionaryResult.Meaning,
            dictionaryResult.CefrLevel,
            DateTime.UtcNow,
            null
        );

        return Result<MasterVocabularyDto>.Success(dto);
    }
}
