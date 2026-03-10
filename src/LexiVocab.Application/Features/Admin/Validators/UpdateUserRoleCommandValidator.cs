using FluentValidation;
using LexiVocab.Application.Features.Admin.Commands;

namespace LexiVocab.Application.Features.Admin.Validators;

public class UpdateUserRoleCommandValidator : AbstractValidator<UpdateUserRoleCommand>
{
    public UpdateUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.");
    }
}
