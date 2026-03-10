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
