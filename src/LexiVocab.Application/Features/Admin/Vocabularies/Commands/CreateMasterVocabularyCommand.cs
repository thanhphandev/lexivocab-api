using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Vocabularies.Commands;

public record CreateMasterVocabularyCommand(
    string Word,
    string? PartOfSpeech,
    string? PhoneticUk,
    string? PhoneticUs,
    string? AudioUrl,
    int? PopularityRank) : IRequest<Result<MasterVocabularyDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(MasterVocabulary);
}

public class CreateMasterVocabularyValidator : AbstractValidator<CreateMasterVocabularyCommand>
{
    public CreateMasterVocabularyValidator()
    {
        RuleFor(x => x.Word).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PartOfSpeech).MaximumLength(50);
        RuleFor(x => x.PhoneticUk).MaximumLength(100);
        RuleFor(x => x.PhoneticUs).MaximumLength(100);
        RuleFor(x => x.AudioUrl).MaximumLength(500);
    }
}

public class CreateMasterVocabularyHandler : IRequestHandler<CreateMasterVocabularyCommand, Result<MasterVocabularyDto>>
{
    private readonly IUnitOfWork _uow;

    public CreateMasterVocabularyHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<MasterVocabularyDto>> Handle(CreateMasterVocabularyCommand request, CancellationToken ct)
    {
        var existing = await _uow.MasterVocabularies.GetByWordAsync(request.Word, ct);
        if (existing != null)
            return Result<MasterVocabularyDto>.Conflict($"Word '{request.Word}' already exists in the master dictionary.");

        var vocab = new MasterVocabulary
        {
            Word = request.Word,
            PartOfSpeech = request.PartOfSpeech,
            PhoneticUk = request.PhoneticUk,
            PhoneticUs = request.PhoneticUs,
            AudioUrl = request.AudioUrl,
            PopularityRank = request.PopularityRank
        };

        _uow.MasterVocabularies.Add(vocab);
        await _uow.SaveChangesAsync(ct);

        return Result<MasterVocabularyDto>.Created(new MasterVocabularyDto(
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
