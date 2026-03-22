using FluentValidation;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Features.Auth.Validators;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Reset code is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.").WithErrorCode(ErrorCode.AUTH_PASSWORD_TOO_WEAK.ToString())
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.").WithErrorCode(ErrorCode.AUTH_PASSWORD_TOO_WEAK.ToString())
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.").WithErrorCode(ErrorCode.AUTH_PASSWORD_TOO_WEAK.ToString())
            .Matches(@"\d").WithMessage("Password must contain at least one digit.").WithErrorCode(ErrorCode.AUTH_PASSWORD_TOO_WEAK.ToString());
    }
}
