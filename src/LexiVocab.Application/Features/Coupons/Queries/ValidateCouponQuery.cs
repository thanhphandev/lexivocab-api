using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Coupons.Queries;

public record CouponValidationResult(
    string Code,
    DiscountType DiscountType,
    decimal DiscountValue,
    string? Currency = null);

public record ValidateCouponQuery(string Code) : IRequest<Result<CouponValidationResult>>;

public class ValidateCouponHandler : IRequestHandler<ValidateCouponQuery, Result<CouponValidationResult>>
{
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _dateTime;

    public ValidateCouponHandler(IUnitOfWork uow, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _dateTime = dateTime;
    }

    public async Task<Result<CouponValidationResult>> Handle(ValidateCouponQuery request, CancellationToken ct)
    {
        var formattedCode = request.Code.Trim().ToUpperInvariant();
        var coupon = await _uow.Coupons.GetByCodeAsync(formattedCode, ct);

        if (coupon == null || !coupon.IsActive)
            return Result<CouponValidationResult>.NotFound("Invalid or inactive coupon code.", ErrorCode.PAYMENT_INVALID_COUPON);

        var now = _dateTime.UtcNow;

        if (coupon.ValidFrom.HasValue && coupon.ValidFrom.Value > now)
            return Result<CouponValidationResult>.Failure("This coupon is not yet valid.", 400, ErrorCode.PAYMENT_INVALID_COUPON);

        if (coupon.ValidUntil.HasValue && coupon.ValidUntil.Value < now)
            return Result<CouponValidationResult>.Failure("This coupon has expired.", 400, ErrorCode.PAYMENT_COUPON_EXPIRED);

        if (coupon.MaxUses.HasValue && coupon.CurrentUses >= coupon.MaxUses.Value)
            return Result<CouponValidationResult>.Failure("This coupon has reached its maximum usage limit.", 400, ErrorCode.PAYMENT_INVALID_COUPON);

        return Result<CouponValidationResult>.Success(new CouponValidationResult(
            coupon.Code,
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.Currency
        ));
    }
}
