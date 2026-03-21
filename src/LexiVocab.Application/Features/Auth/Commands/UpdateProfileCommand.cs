using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Auth.Commands;

public record UpdateProfileCommand(
    string FullName,
    string? AvatarUrl = null
) : IRequest<Result<UserProfileDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.UserUpdated;
    public string? EntityType => "User";
}

public class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AvatarUrl).Must(url => string.IsNullOrEmpty(url) || url.StartsWith("https://api.dicebear.com/") || url.StartsWith("https://lh3.googleusercontent.com/"))
            .WithMessage("Avatar URL must be a valid DiceBear or Google image URL.");
    }
}

public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, Result<UserProfileDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateProfileHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<UserProfileDto>> Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Result<UserProfileDto>.NotFound("User not found in context.");

        var user = await _uow.Users.GetByIdAsync(userId.Value, ct);
        if (user == null)
            return Result<UserProfileDto>.NotFound("User account no longer exists.");

        user.FullName = request.FullName.Trim();
        if (request.AvatarUrl != null)
        {
            user.AvatarUrl = request.AvatarUrl;
        }
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<UserProfileDto>.Success(new UserProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt,
            user.LastLogin,
            user.AvatarUrl));
    }
}
