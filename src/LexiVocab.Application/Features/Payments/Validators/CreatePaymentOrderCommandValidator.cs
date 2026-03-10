using FluentValidation;
using LexiVocab.Application.Features.Payments.Commands;

namespace LexiVocab.Application.Features.Payments.Validators;

public class CreatePaymentOrderCommandValidator : AbstractValidator<CreatePaymentOrderCommand>
{
    public CreatePaymentOrderCommandValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty().WithMessage("Plan ID is required.");

        RuleFor(x => x.Provider)
            .IsInEnum().WithMessage("Invalid payment provider.");
    }
}
