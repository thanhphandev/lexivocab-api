using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Vocabularies.Queries;

// ─── Get Paginated Vocabulary List ──────────────────────────────
public record GetVocabularyListQuery(
    int Page = 1,
    int PageSize = 20,
    bool? IsArchived = null,
    string? SearchTerm = null
) : IRequest<Result<PagedResult<VocabularyDto>>>;

public class GetVocabularyListHandler : IRequestHandler<GetVocabularyListQuery, Result<PagedResult<VocabularyDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetVocabularyListHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<VocabularyDto>>> Handle(GetVocabularyListQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var (items, totalCount) = await _uow.Vocabularies.GetByUserIdAsync(
            userId, request.Page, request.PageSize, request.IsArchived, request.SearchTerm, ct);

        var dtos = items.Select(MapToDto).ToList();

        return Result<PagedResult<VocabularyDto>>.Success(new PagedResult<VocabularyDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    private static VocabularyDto MapToDto(UserVocabulary v) => new(
        v.Id, v.WordText, v.CustomMeaning, v.ContextSentence, v.SourceUrl,
        v.RepetitionCount, v.EasinessFactor, v.IntervalDays,
        v.NextReviewDate, v.LastReviewedAt, v.IsArchived, v.CreatedAt,
        v.MasterVocabulary?.PhoneticUk, v.MasterVocabulary?.PhoneticUs,
        v.MasterVocabulary?.AudioUrl, v.MasterVocabulary?.PartOfSpeech);
}

// ─── Get Single Vocabulary by Id ────────────────────────────────
public record GetVocabularyByIdQuery(Guid Id) : IRequest<Result<VocabularyDto>>;

public class GetVocabularyByIdHandler : IRequestHandler<GetVocabularyByIdQuery, Result<VocabularyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetVocabularyByIdHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<VocabularyDto>> Handle(GetVocabularyByIdQuery request, CancellationToken ct)
    {
        var entity = await _uow.Vocabularies.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != _currentUser.UserId)
            return Result<VocabularyDto>.NotFound("Vocabulary not found.");

        return Result<VocabularyDto>.Success(new VocabularyDto(
            entity.Id, entity.WordText, entity.CustomMeaning, entity.ContextSentence, entity.SourceUrl,
            entity.RepetitionCount, entity.EasinessFactor, entity.IntervalDays,
            entity.NextReviewDate, entity.LastReviewedAt, entity.IsArchived, entity.CreatedAt,
            entity.MasterVocabulary?.PhoneticUk, entity.MasterVocabulary?.PhoneticUs,
            entity.MasterVocabulary?.AudioUrl, entity.MasterVocabulary?.PartOfSpeech));
    }
}

// ─── Get Vocabulary Stats ───────────────────────────────────────
public record GetVocabularyStatsQuery : IRequest<Result<VocabularyStatsDto>>;

public class GetVocabularyStatsHandler : IRequestHandler<GetVocabularyStatsQuery, Result<VocabularyStatsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetVocabularyStatsHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<VocabularyStatsDto>> Handle(GetVocabularyStatsQuery request, CancellationToken ct)
    {
        var (total, active, archived, dueToday) = await _uow.Vocabularies.GetStatsAsync(
            _currentUser.UserId!.Value, ct);

        return Result<VocabularyStatsDto>.Success(new VocabularyStatsDto(total, active, archived, dueToday));
    }
}
