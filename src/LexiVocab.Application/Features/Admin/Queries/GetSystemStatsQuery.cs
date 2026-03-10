using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Queries;

public record GetSystemStatsQuery() : IRequest<Result<SystemStatsDto>>;

public class GetSystemStatsHandler : IRequestHandler<GetSystemStatsQuery, Result<SystemStatsDto>>
{
    private readonly IUnitOfWork _uow;

    public GetSystemStatsHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<SystemStatsDto>> Handle(GetSystemStatsQuery request, CancellationToken ct)
    {
        var totalUsers = await _uow.Users.CountAsync(ct);
        var totalPremiumUsers = await _uow.Users.CountPremiumUsersAsync(ct);
        var totalVocabularies = await _uow.Vocabularies.CountAsync(ct);
        var totalReviews = await _uow.ReviewLogs.CountAsync(ct);
        var totalActiveSubs = await _uow.Subscriptions.CountActiveAsync(ct);

        return Result<SystemStatsDto>.Success(new SystemStatsDto(
            TotalUsers: totalUsers,
            TotalPremiumUsers: totalPremiumUsers,
            TotalVocabularies: totalVocabularies,
            TotalReviews: totalReviews,
            TotalActiveSubscriptions: totalActiveSubs
        ));
    }
}
