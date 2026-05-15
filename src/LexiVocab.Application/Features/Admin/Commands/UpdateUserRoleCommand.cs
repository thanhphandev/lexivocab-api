using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Commands;

public record UpdateUserRoleCommand(Guid UserId, string Role) : IRequest<Result<string>>;

public class UpdateUserRoleHandler : IRequestHandler<UpdateUserRoleCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateUserRoleHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<string>> Handle(UpdateUserRoleCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null) return Result<string>.Failure("User not found", 404);

        // Security: Prevents an Admin from changing their own role or another Admin's role
        if (user.Id == _currentUser.GetRequiredUserId())
            return Result<string>.Forbidden("You cannot change your own role.", LexiVocab.Domain.Enums.ErrorCode.AUTHZ_ADMIN_ONLY);

        if (user.Role == LexiVocab.Domain.Enums.UserRole.Admin)
            return Result<string>.Forbidden("You cannot modify the role of another administrator.", LexiVocab.Domain.Enums.ErrorCode.AUTHZ_ADMIN_ONLY);

        if (!Enum.TryParse<UserRole>(request.Role, true, out var roleEnum))
        {
            return Result<string>.Failure("Invalid role. Must be 'User' or 'Admin'.", 400);
        }

        user.Role = roleEnum;
        user.UpdatedAt = _dateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success($"User role updated to {roleEnum}");
    }
}
