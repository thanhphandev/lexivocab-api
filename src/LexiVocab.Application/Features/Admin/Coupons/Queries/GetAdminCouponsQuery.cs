using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Coupons.Queries;

public record GetAdminCouponsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null) : IRequest<Result<PagedResult<AdminCouponDto>>>;

public class GetAdminCouponsHandler : IRequestHandler<GetAdminCouponsQuery, Result<PagedResult<AdminCouponDto>>>
{
    private readonly IUnitOfWork _uow;

    public GetAdminCouponsHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<PagedResult<AdminCouponDto>>> Handle(GetAdminCouponsQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _uow.Coupons.GetPagedAsync(request.Search, request.Page, request.PageSize, ct);

        var dtos = items.Select(coupon => new AdminCouponDto(
            coupon.Id,
            coupon.Code,
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.MaxUses,
            coupon.CurrentUses,
            coupon.ValidFrom,
            coupon.ValidUntil,
            coupon.Currency,
            coupon.IsActive,
            coupon.Description,
            coupon.CreatedAt,
            coupon.UpdatedAt
        )).ToList();

        var pagedResult = new PagedResult<AdminCouponDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<AdminCouponDto>>.Success(pagedResult);
    }
}
