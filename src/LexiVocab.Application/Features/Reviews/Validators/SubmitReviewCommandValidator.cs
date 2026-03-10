using FluentValidation;
using LexiVocab.Application.Features.Reviews.Commands;

namespace LexiVocab.Application.Features.Reviews.Validators;

public class SubmitReviewCommandValidator : AbstractValidator<SubmitReviewCommand>
{
    public SubmitReviewCommandValidator()
    {
        RuleFor(x => x.UserVocabularyId)
            .NotEmpty().WithMessage("Vocabulary ID is required.");

        RuleFor(x => x.QualityScore)
            .IsInEnum().WithMessage("Invalid quality score.");

        RuleFor(x => x.TimeSpentMs)
            .GreaterThanOrEqualTo(0).When(x => x.TimeSpentMs.HasValue)
            .WithMessage("Time spent must be positive.");
    }
}
