using FluentValidation;
using LexiVocab.Application.Features.Vocabularies.Commands;

namespace LexiVocab.Application.Features.Vocabularies.Validators;

public class CreateVocabularyValidator : AbstractValidator<CreateVocabularyCommand>
{
    public CreateVocabularyValidator()
    {
        RuleFor(x => x.WordText)
            .NotEmpty().WithMessage("Word text is required.")
            .MaximumLength(100).WithMessage("Word text cannot exceed 100 characters.");

        RuleFor(x => x.CustomMeaning)
            .MaximumLength(500).WithMessage("Meaning cannot exceed 500 characters.");

        RuleFor(x => x.ContextSentence)
            .MaximumLength(1000).WithMessage("Context sentence cannot exceed 1000 characters.");

        RuleFor(x => x.SourceUrl)
            .MaximumLength(2083).WithMessage("Source URL is too long.")
            .Must(uri => string.IsNullOrEmpty(uri) || Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("Invalid URL format.");
    }
}
