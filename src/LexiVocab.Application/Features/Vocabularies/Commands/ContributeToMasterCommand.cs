using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Vocabularies.Commands;

public record ContributeToMasterCommand(Guid VocabularyId) : IRequest<Result<bool>>;

public class ContributeToMasterHandler : IRequestHandler<ContributeToMasterCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ContributeToMasterHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(ContributeToMasterCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        
        var userVocab = await _uow.Vocabularies.GetByIdAsync(request.VocabularyId, ct);
        
        if (userVocab == null || userVocab.UserId != userId)
            return Result<bool>.NotFound($"Vocabulary with ID {request.VocabularyId} not found.", LexiVocab.Domain.Enums.ErrorCode.VOCAB_NOT_FOUND);

        if (userVocab.MasterVocabularyId.HasValue)
            return Result<bool>.Success(true); // Already linked/contributed

        var wordLower = userVocab.WordText.ToLowerInvariant().Trim();
        var existingMaster = await _uow.MasterVocabularies.GetByWordAsync(wordLower, ct);

        if (existingMaster != null)
        {
            // Already exists in master, just link it
            userVocab.MasterVocabularyId = existingMaster.Id;
            _uow.Vocabularies.Update(userVocab);
        }
        else
        {
            // Needs to be contributed as pending
            var newMaster = new MasterVocabulary
            {
                Word = wordLower,
                IsApproved = false,
                CreatedByUserId = userId
            };
            await _uow.MasterVocabularies.AddAsync(newMaster, ct);
            // Link object reference so EF Core resolves the ID upon save
            userVocab.MasterVocabulary = newMaster;
        }

        await _uow.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
