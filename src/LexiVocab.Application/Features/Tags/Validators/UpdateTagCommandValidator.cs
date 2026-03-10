using FluentValidation;
using LexiVocab.Application.Features.Tags.Commands;

namespace LexiVocab.Application.Features.Tags.Validators;

public class UpdateTagCommandValidator : AbstractValidator<UpdateTagCommand>
{
    public UpdateTagCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Tag ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tag name is required.")
            .MaximumLength(100).WithMessage("Tag name must not exceed 100 characters.");

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("Color code must not exceed 20 characters.")
            .When(x => x.Color is not null);

        RuleFor(x => x.Icon)
            .MaximumLength(20).WithMessage("Icon must not exceed 20 characters.")
            .When(x => x.Icon is not null);
            
        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Display order must be a non-negative integer.");
    }
}
