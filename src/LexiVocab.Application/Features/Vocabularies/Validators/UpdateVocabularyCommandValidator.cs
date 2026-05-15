using FluentValidation;
using LexiVocab.Application.Features.Vocabularies.Commands;

namespace LexiVocab.Application.Features.Vocabularies.Validators;

public class UpdateVocabularyCommandValidator : AbstractValidator<UpdateVocabularyCommand>
{
    public UpdateVocabularyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.CustomMeaning)
            .MaximumLength(500)
            .When(x => x.CustomMeaning != null);

        RuleFor(x => x.ContextSentence)
            .MaximumLength(1000)
            .When(x => x.ContextSentence != null);
    }
}
