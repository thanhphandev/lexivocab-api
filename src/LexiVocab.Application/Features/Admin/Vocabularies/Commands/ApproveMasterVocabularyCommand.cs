using LexiVocab.Application.Common;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Vocabularies.Commands;

public record ApproveMasterVocabularyCommand(Guid Id, string? Meaning = null, string? PhoneticUk = null, string? PhoneticUs = null, string? AudioUrl = null) : IRequest<Result<bool>>;

public class ApproveMasterVocabularyHandler : IRequestHandler<ApproveMasterVocabularyCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;

    public ApproveMasterVocabularyHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(ApproveMasterVocabularyCommand request, CancellationToken ct)
    {
        var masterVocab = await _uow.MasterVocabularies.GetByIdAsync(request.Id, ct);
        if (masterVocab == null)
            return Result<bool>.NotFound("Master vocabulary not found.");

        if (masterVocab.IsApproved)
            return Result<bool>.Failure("Master vocabulary is already approved.");

        masterVocab.IsApproved = true;
        
        // Admin can optionally correct the meaning/phonetics when approving
        if (!string.IsNullOrWhiteSpace(request.Meaning))
            masterVocab.Meaning = request.Meaning;
        
        if (!string.IsNullOrWhiteSpace(request.PhoneticUk))
            masterVocab.PhoneticUk = request.PhoneticUk;
            
        if (!string.IsNullOrWhiteSpace(request.PhoneticUs))
            masterVocab.PhoneticUs = request.PhoneticUs;
            
        if (!string.IsNullOrWhiteSpace(request.AudioUrl))
            masterVocab.AudioUrl = request.AudioUrl;

        _uow.MasterVocabularies.Update(masterVocab);
        await _uow.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
