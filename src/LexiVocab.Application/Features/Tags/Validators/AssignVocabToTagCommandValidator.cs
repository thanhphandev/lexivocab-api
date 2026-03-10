using FluentValidation;
using LexiVocab.Application.Features.Tags.Commands;

namespace LexiVocab.Application.Features.Tags.Validators;

public class AssignVocabToTagCommandValidator : AbstractValidator<AssignVocabToTagCommand>
{
    public AssignVocabToTagCommandValidator()
    {
        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage("Tag ID is required.");
            
        RuleFor(x => x.VocabularyId)
            .NotEmpty().WithMessage("Vocabulary ID is required.");
    }
}
