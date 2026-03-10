using FluentValidation;
using LexiVocab.Application.Features.Payments.Commands;

namespace LexiVocab.Application.Features.Payments.Validators;

public class CapturePaymentOrderCommandValidator : AbstractValidator<CapturePaymentOrderCommand>
{
    public CapturePaymentOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("Order ID is required.");
    }
}
