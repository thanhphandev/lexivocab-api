using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;
using LexiVocab.Domain.Entities;

namespace LexiVocab.Application.Features.Admin.Vocabularies.Commands;

public record UpdateMasterVocabularyCommand(
    Guid Id,
    string Word,
    string? PartOfSpeech,
    string? PhoneticUk,
    string? PhoneticUs,
    string? AudioUrl,
    int? PopularityRank) : IRequest<Result<MasterVocabularyDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(MasterVocabulary);
    public string EntityId => Id.ToString();
}

public class UpdateMasterVocabularyValidator : AbstractValidator<UpdateMasterVocabularyCommand>
{
    public UpdateMasterVocabularyValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Word).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PartOfSpeech).MaximumLength(50);
        RuleFor(x => x.PhoneticUk).MaximumLength(100);
        RuleFor(x => x.PhoneticUs).MaximumLength(100);
        RuleFor(x => x.AudioUrl).MaximumLength(500);
    }
}

public class UpdateMasterVocabularyHandler : IRequestHandler<UpdateMasterVocabularyCommand, Result<MasterVocabularyDto>>
{
    private readonly IUnitOfWork _uow;

    public UpdateMasterVocabularyHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<MasterVocabularyDto>> Handle(UpdateMasterVocabularyCommand request, CancellationToken ct)
    {
        var existing = await _uow.MasterVocabularies.GetByIdAsync(request.Id, ct);
        if (existing == null)
            return Result<MasterVocabularyDto>.NotFound($"Vocabulary with ID '{request.Id}' not found.");

        if (existing.Word != request.Word)
        {
            var wordExists = await _uow.MasterVocabularies.GetByWordAsync(request.Word, ct);
            if (wordExists != null)
                return Result<MasterVocabularyDto>.Conflict($"Word '{request.Word}' already exists.");
        }

        existing.Word = request.Word;
        existing.PartOfSpeech = request.PartOfSpeech;
        existing.PhoneticUk = request.PhoneticUk;
        existing.PhoneticUs = request.PhoneticUs;
        existing.AudioUrl = request.AudioUrl;
        existing.PopularityRank = request.PopularityRank;

        _uow.MasterVocabularies.Update(existing);
        await _uow.SaveChangesAsync(ct);

        return Result<MasterVocabularyDto>.Success(new MasterVocabularyDto(
            existing.Id,
            existing.Word,
            existing.PartOfSpeech,
            existing.PhoneticUk,
            existing.PhoneticUs,
            existing.AudioUrl,
            existing.PopularityRank,
            existing.CreatedAt,
            existing.UpdatedAt));
    }
}
