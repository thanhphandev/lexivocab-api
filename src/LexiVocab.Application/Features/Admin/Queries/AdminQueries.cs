using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Queries;

public record GetUsersQuery(int Page = 1, int PageSize = 20, string? Search = null) 
    : IRequest<Result<PagedResult<UserOverviewDto>>>;

public class GetUsersHandler : IRequestHandler<GetUsersQuery, Result<PagedResult<UserOverviewDto>>>
{
    private readonly IUnitOfWork _uow;

    public GetUsersHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<PagedResult<UserOverviewDto>>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _uow.Users.GetPaginatedAsync(request.Page, request.PageSize, request.Search, ct);

        var dtos = items.Select(u => new UserOverviewDto(
            u.Id,
            u.Email,
            u.FullName,
            u.Role.ToString(),
            u.IsActive,
            u.LastLogin,
            u.CreatedAt,
            u.AuthProvider)).ToList();

        return Result<PagedResult<UserOverviewDto>>.Success(new PagedResult<UserOverviewDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}

public record GetUserDetailQuery(Guid UserId) : IRequest<Result<UserDetailDto>>;

public class GetUserDetailHandler : IRequestHandler<GetUserDetailQuery, Result<UserDetailDto>>
{
    private readonly IUnitOfWork _uow;

    public GetUserDetailHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<UserDetailDto>> Handle(GetUserDetailQuery request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);

        if (user == null)
            return Result<UserDetailDto>.Failure("User not found", 404);

        var totalVocabularies = await _uow.Vocabularies.CountByUserIdAsync(request.UserId, ct);
        var totalReviews = await _uow.ReviewLogs.CountByUserIdAsync(request.UserId, ct);

        var subs = await _uow.Subscriptions.GetByUserIdAsync(request.UserId, ct);
        
        var subscriptionDtos = subs.Select(s => new AdminSubscriptionDto(
            s.Id,
            s.Plan.ToString(),
            s.Status.ToString(),
            s.StartDate,
            s.EndDate,
            s.Provider.ToString(),
            s.ExternalSubscriptionId)).ToList();

        var dto = new UserDetailDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            user.IsActive,
            user.LastLogin,
            user.CreatedAt,
            user.AuthProvider,
            totalVocabularies,
            totalReviews,
            user.PlanExpirationDate,
            subscriptionDtos);

        return Result<UserDetailDto>.Success(dto);
    }
}

// ─── System Stats Overview ────────────────────────────────────────
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
