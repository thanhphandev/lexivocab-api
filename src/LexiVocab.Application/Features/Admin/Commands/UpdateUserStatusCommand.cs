using LexiVocab.Application.Common;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Commands;

public record UpdateUserStatusCommand(Guid UserId, bool IsActive) : IRequest<Result<string>>;

public class UpdateUserStatusHandler : IRequestHandler<UpdateUserStatusCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;

    public UpdateUserStatusHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<string>> Handle(UpdateUserStatusCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null) return Result<string>.Failure("User not found", 404);

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        var statusString = request.IsActive ? "Activated" : "Deactivated";
        return Result<string>.Success($"User successfully {statusString}");
    }
}
