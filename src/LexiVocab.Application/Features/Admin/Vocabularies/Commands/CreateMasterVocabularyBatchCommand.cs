using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Vocabularies.Commands;

public record CreateMasterVocabularyBatchItemDto(
    string Word,
    string? PartOfSpeech,
    string? PhoneticUk,
    string? PhoneticUs,
    string? Meaning,
    string? CefrLevel);

public record CreateMasterVocabularyBatchCommand(
    List<CreateMasterVocabularyBatchItemDto> Items) : IRequest<Result<int>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(MasterVocabulary);
}

public class CreateMasterVocabularyBatchValidator : AbstractValidator<CreateMasterVocabularyBatchCommand>
{
    public CreateMasterVocabularyBatchValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Items list cannot be empty.");
        RuleFor(x => x.Items).Must(x => x.Count <= 500).WithMessage("Cannot import more than 500 words at a time.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Word).NotEmpty().MaximumLength(100);
            item.RuleFor(x => x.PartOfSpeech).MaximumLength(50);
            item.RuleFor(x => x.PhoneticUk).MaximumLength(100);
            item.RuleFor(x => x.PhoneticUs).MaximumLength(100);
            item.RuleFor(x => x.CefrLevel).MaximumLength(10);
        });
    }
}

public class CreateMasterVocabularyBatchHandler : IRequestHandler<CreateMasterVocabularyBatchCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;

    public CreateMasterVocabularyBatchHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<int>> Handle(CreateMasterVocabularyBatchCommand request, CancellationToken ct)
    {
        var inputWords = request.Items.Select(x => x.Word.ToLowerInvariant().Trim()).Distinct().ToList();
        
        // Fetch existing using single query
        var existingDict = await _uow.MasterVocabularies.GetByWordsAsync(inputWords, ct);

        var newVocabs = new List<MasterVocabulary>();

        foreach (var reqItem in request.Items)
        {
            var cleanWord = reqItem.Word.ToLowerInvariant().Trim();
            
            if (existingDict.ContainsKey(cleanWord))
                continue; // Skip already existing words
                
            // Also ensure we don't add duplicates from the request itself
            if (newVocabs.Any(x => x.Word == cleanWord))
                continue;
                
            var vocab = new MasterVocabulary
            {
                Word = cleanWord,
                PartOfSpeech = reqItem.PartOfSpeech,
                PhoneticUk = reqItem.PhoneticUk,
                PhoneticUs = reqItem.PhoneticUs,
                Meaning = reqItem.Meaning,
                CefrLevel = reqItem.CefrLevel
            };
            
            newVocabs.Add(vocab);
        }

        if (newVocabs.Count > 0)
        {
            foreach (var vocab in newVocabs)
            {
                _uow.MasterVocabularies.Add(vocab);
            }
            await _uow.SaveChangesAsync(ct);
        }

        return Result<int>.Created(newVocabs.Count);
    }
}
