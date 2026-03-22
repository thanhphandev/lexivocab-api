using FluentValidation;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Features.Vocabularies.Validators;

public class BatchImportValidator : AbstractValidator<BatchImportCommand>
{
    public BatchImportValidator()
    {
        RuleFor(x => x.Words)
            .NotEmpty().WithMessage("Import list cannot be empty.").WithErrorCode(ErrorCode.VOCAB_INVALID_IMPORT_FORMAT.ToString())
            .Must(x => x.Count <= 50).WithMessage("Maximum 50 words allowed per batch import.").WithErrorCode(ErrorCode.VOCAB_INVALID_IMPORT_FORMAT.ToString());

        RuleForEach(x => x.Words).SetValidator(new CreateVocabularyValidator());
    }
}
