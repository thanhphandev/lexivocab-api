using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Tags.Commands;

public record AssignVocabToTagCommand(Guid TagId, Guid VocabularyId) : IRequest<Result>;

public class AssignVocabToTagHandler : IRequestHandler<AssignVocabToTagCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public AssignVocabToTagHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(AssignVocabToTagCommand request, CancellationToken ct)
    {
        var vocab = await _uow.Vocabularies.GetByIdAsync(request.VocabularyId, ct);
        if (vocab is null || vocab.UserId != _currentUser.UserId)
            return Result.NotFound("Vocabulary not found.");
            
        var tag = await _uow.Tags.GetByIdAsync(request.TagId, ct);
        if (tag is null || tag.UserId != _currentUser.UserId)
            return Result.NotFound("Tag not found.");

        vocab.TagId = request.TagId;
        _uow.Vocabularies.Update(vocab);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
