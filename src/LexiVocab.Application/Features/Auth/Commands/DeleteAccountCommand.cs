using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Auth.Commands;

public record DeleteAccountCommand : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.UserDeleted;
    public string? EntityType => "User";
}

public class DeleteAccountHandler : IRequestHandler<DeleteAccountCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DeleteAccountHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteAccountCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Result.NotFound("User not found in context.");

        var user = await _uow.Users.GetByIdAsync(userId.Value, ct);
        if (user == null)
            return Result.NotFound("User account no longer exists.");

        // Entity Framework Core will automatically handle cascade deletes
        // for UserVocabularies, ReviewLogs, Subscriptions, Tags, and Settings 
        // given how the foreign keys are set up.
        _uow.Users.Remove(user);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
