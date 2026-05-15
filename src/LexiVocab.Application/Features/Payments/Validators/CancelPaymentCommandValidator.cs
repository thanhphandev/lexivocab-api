using FluentValidation;
using LexiVocab.Application.Features.Payments.Commands;

namespace LexiVocab.Application.Features.Payments.Validators;

public class CancelPaymentCommandValidator : AbstractValidator<CancelPaymentCommand>
{
    public CancelPaymentCommandValidator()
    {
        RuleFor(x => x.Reference)
            .NotEmpty()
            .WithMessage("Payment reference is required to cancel.");
    }
}
