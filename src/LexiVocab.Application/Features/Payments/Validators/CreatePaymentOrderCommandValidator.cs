using FluentValidation;
using LexiVocab.Application.Features.Payments.Commands;

namespace LexiVocab.Application.Features.Payments.Validators;

public class CreatePaymentOrderCommandValidator : AbstractValidator<CreatePaymentOrderCommand>
{
    public CreatePaymentOrderCommandValidator()
    {
        RuleFor(x => x.PricingId)
            .NotEmpty().WithMessage("Pricing ID is required.");

        RuleFor(x => x.Provider)
            .IsInEnum().WithMessage("Invalid payment provider.");
    }
}
