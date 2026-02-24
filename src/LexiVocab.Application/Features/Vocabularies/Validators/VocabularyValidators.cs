using FluentValidation;
using LexiVocab.Application.Features.Vocabularies.Commands;

namespace LexiVocab.Application.Features.Vocabularies.Validators;

public class CreateVocabularyValidator : AbstractValidator<CreateVocabularyCommand>
{
    public CreateVocabularyValidator()
    {
        RuleFor(x => x.WordText)
            .NotEmpty().WithMessage("Word text is required.")
            .MaximumLength(100).WithMessage("Word text must not exceed 100 characters.");

        RuleFor(x => x.CustomMeaning)
            .MaximumLength(500).When(x => x.CustomMeaning is not null);

        RuleFor(x => x.SourceUrl)
            .MaximumLength(2048).When(x => x.SourceUrl is not null);
    }
}
