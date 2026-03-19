using LexiVocab.Application.Common;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Public.Coupons.Queries;

public record CouponValidationResult(
    string Code,
    DiscountType DiscountType,
    decimal DiscountValue);

public record ValidateCouponQuery(string Code) : IRequest<Result<CouponValidationResult>>;

public class ValidateCouponHandler : IRequestHandler<ValidateCouponQuery, Result<CouponValidationResult>>
{
    private readonly IUnitOfWork _uow;

    public ValidateCouponHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<CouponValidationResult>> Handle(ValidateCouponQuery request, CancellationToken ct)
    {
        var formattedCode = request.Code.Trim().ToUpperInvariant();
        var coupon = await _uow.Coupons.GetByCodeAsync(formattedCode, ct);

        if (coupon == null || !coupon.IsActive)
            return Result<CouponValidationResult>.NotFound("Invalid or inactive coupon code.");

        var now = DateTime.UtcNow;

        if (coupon.ValidFrom.HasValue && coupon.ValidFrom.Value > now)
            return Result<CouponValidationResult>.Failure("This coupon is not yet valid.");

        if (coupon.ValidUntil.HasValue && coupon.ValidUntil.Value < now)
            return Result<CouponValidationResult>.Failure("This coupon has expired.");

        if (coupon.MaxUses.HasValue && coupon.CurrentUses >= coupon.MaxUses.Value)
            return Result<CouponValidationResult>.Failure("This coupon has reached its maximum usage limit.");

        return Result<CouponValidationResult>.Success(new CouponValidationResult(
            coupon.Code,
            coupon.DiscountType,
            coupon.DiscountValue
        ));
    }
}
