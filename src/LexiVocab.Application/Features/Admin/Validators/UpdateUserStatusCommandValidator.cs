using FluentValidation;
using LexiVocab.Application.Features.Admin.Commands;

namespace LexiVocab.Application.Features.Admin.Validators;

public class UpdateUserStatusCommandValidator : AbstractValidator<UpdateUserStatusCommand>
{
    public UpdateUserStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
