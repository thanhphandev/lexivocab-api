using FluentValidation;
using LexiVocab.Application.Features.Tags.Commands;

namespace LexiVocab.Application.Features.Tags.Validators;

public class DeleteTagCommandValidator : AbstractValidator<DeleteTagCommand>
{
    public DeleteTagCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Tag ID is required.");
    }
}
