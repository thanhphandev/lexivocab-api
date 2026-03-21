using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Coupons.Commands;

public record CreateCouponCommand(
    string Code,
    DiscountType DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    string? Currency,
    bool IsActive,
    string? Description) : IRequest<Result<AdminCouponDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(Domain.Entities.Coupon);
}

public class CreateCouponValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DiscountType).IsInEnum();
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.MaxUses).GreaterThan(0).When(x => x.MaxUses.HasValue);
        RuleFor(x => x.ValidFrom).LessThan(x => x.ValidUntil).When(x => x.ValidFrom.HasValue && x.ValidUntil.HasValue);
    }
}

public class CreateCouponHandler : IRequestHandler<CreateCouponCommand, Result<AdminCouponDto>>
{
    private readonly IUnitOfWork _uow;

    public CreateCouponHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<AdminCouponDto>> Handle(CreateCouponCommand request, CancellationToken ct)
    {
        var formattedCode = request.Code.Trim().ToUpperInvariant();
        
        var existing = await _uow.Coupons.GetByCodeAsync(formattedCode, ct);
        if (existing != null)
            return Result<AdminCouponDto>.Conflict($"Coupon with code '{formattedCode}' already exists.");

        var coupon = new Coupon
        {
            Code = formattedCode,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MaxUses = request.MaxUses,
            CurrentUses = 0,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            Currency = request.DiscountType == DiscountType.FixedAmount ? request.Currency : null,
            IsActive = request.IsActive,
            Description = request.Description?.Trim()
        };

        await _uow.Coupons.AddAsync(coupon, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<AdminCouponDto>.Created(new AdminCouponDto(
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
        ));
    }
}
