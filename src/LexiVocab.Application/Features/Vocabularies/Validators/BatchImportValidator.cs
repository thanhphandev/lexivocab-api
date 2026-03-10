using FluentValidation;
using LexiVocab.Application.Features.Vocabularies.Commands;

namespace LexiVocab.Application.Features.Vocabularies.Validators;

public class BatchImportValidator : AbstractValidator<BatchImportCommand>
{
    public BatchImportValidator()
    {
        RuleFor(x => x.Words)
            .NotEmpty().WithMessage("Import list cannot be empty.")
            .Must(x => x.Count <= 50).WithMessage("Maximum 50 words allowed per batch import.");

        RuleForEach(x => x.Words).SetValidator(new CreateVocabularyValidator());
    }
}
