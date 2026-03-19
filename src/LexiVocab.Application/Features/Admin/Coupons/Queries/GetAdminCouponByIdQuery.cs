using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Coupons.Queries;

public record GetAdminCouponByIdQuery(Guid Id) : IRequest<Result<AdminCouponDto>>;

public class GetAdminCouponByIdHandler : IRequestHandler<GetAdminCouponByIdQuery, Result<AdminCouponDto>>
{
    private readonly IUnitOfWork _uow;

    public GetAdminCouponByIdHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<AdminCouponDto>> Handle(GetAdminCouponByIdQuery request, CancellationToken ct)
    {
        var coupon = await _uow.Coupons.GetByIdAsync(request.Id, ct);
        if (coupon == null)
            return Result<AdminCouponDto>.NotFound($"Coupon with ID '{request.Id}' not found.");

        var dto = new AdminCouponDto(
            coupon.Id,
            coupon.Code,
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.MaxUses,
            coupon.CurrentUses,
            coupon.ValidFrom,
            coupon.ValidUntil,
            coupon.IsActive,
            coupon.Description,
            coupon.CreatedAt,
            coupon.UpdatedAt);

        return Result<AdminCouponDto>.Success(dto);
    }
}
