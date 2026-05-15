using FluentValidation;
using LexiVocab.Application.Features.Vocabularies.Commands;

namespace LexiVocab.Application.Features.Vocabularies.Validators;

public class DeleteVocabularyCommandValidator : AbstractValidator<DeleteVocabularyCommand>
{
    public DeleteVocabularyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}
