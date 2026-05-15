using FluentValidation;
using LexiVocab.Application.Features.Reviews.Commands;

namespace LexiVocab.Application.Features.Reviews.Validators;

public class SubmitReviewCommandValidator : AbstractValidator<SubmitReviewCommand>
{
    public SubmitReviewCommandValidator()
    {
        RuleFor(x => x.UserVocabularyId)
            .NotEmpty();

        RuleFor(x => x.QualityScore)
            .IsInEnum()
            .WithMessage("Invalid quality score. Must be between 0 (Blackout) and 5 (Perfect).");

        RuleFor(x => x.TimeSpentMs)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TimeSpentMs.HasValue)
            .WithMessage("Time spent cannot be negative.");
    }
}
