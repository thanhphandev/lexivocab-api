using FluentValidation;
using LexiVocab.Application.Features.Admin.Commands;

namespace LexiVocab.Application.Features.Admin.Validators;

public class ImpersonateUserCommandValidator : AbstractValidator<ImpersonateUserCommand>
{
    public ImpersonateUserCommandValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("Target user ID is required.");
    }
}
