using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Enums;
using MediatR;

namespace LexiVocab.Application.Features.Tags.Commands;

public record DeleteTagCommand(Guid Id) : IRequest<Result>;

public class DeleteTagHandler : IRequestHandler<DeleteTagCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DeleteTagHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteTagCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var entity = await _uow.Tags.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != userId)
            return Result.NotFound("Tag not found.", ErrorCode.TAG_NOT_FOUND);
        if (entity.WordCount > 0)
            return Result.Conflict("Cannot delete a tag that has words associated with it.", ErrorCode.TAG_CANNOT_DELETE_WITH_WORDS);

        _uow.Tags.Remove(entity);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
