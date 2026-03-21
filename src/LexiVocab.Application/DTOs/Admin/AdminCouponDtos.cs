using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.DTOs.Admin;

public record AdminCouponDto(
    Guid Id,
    string Code,
    DiscountType DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    int CurrentUses,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    string? Currency,
    bool IsActive,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateCouponRequest(
    string Code,
    DiscountType DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    string? Currency,
    bool IsActive,
    string? Description
);

public record UpdateCouponRequest(
    string Code,
    DiscountType DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    string? Currency,
    bool IsActive,
    string? Description
);
