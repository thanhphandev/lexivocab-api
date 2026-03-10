using FluentValidation;
using LexiVocab.Application.Features.Auth.Commands;

namespace LexiVocab.Application.Features.Auth.Validators;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
