using FluentValidation;
using LexiVocab.Application.Features.Auth.Commands;

namespace LexiVocab.Application.Features.Auth.Validators;

public class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty();
            
        RuleFor(x => x.DeviceInfo)
            .NotEmpty();
    }
}
