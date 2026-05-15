using FluentValidation;
using LexiVocab.Application.Features.Auth.Commands;

namespace LexiVocab.Application.Features.Auth.Validators;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
            .Matches(@"[\^$*.\[\]{}()?\-!@#%&/\\,><':;|_~`]").WithMessage("Password must contain at least one special character.");
    }
}
