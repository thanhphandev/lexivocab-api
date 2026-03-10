using LexiVocab.Application.Common;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Commands;

public record UpdateUserRoleCommand(Guid UserId, string Role) : IRequest<Result<string>>;

public class UpdateUserRoleHandler : IRequestHandler<UpdateUserRoleCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;

    public UpdateUserRoleHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<string>> Handle(UpdateUserRoleCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null) return Result<string>.Failure("User not found", 404);

        if (!Enum.TryParse<UserRole>(request.Role, true, out var roleEnum))
        {
            return Result<string>.Failure("Invalid role. Must be 'User' or 'Admin'.", 400);
        }

        user.Role = roleEnum;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success($"User role updated to {roleEnum}");
    }
}
