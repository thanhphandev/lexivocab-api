using System.Collections.Generic;

namespace LexiVocab.Application.DTOs.Admin;

public record UserOverviewDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTime? LastLogin,
    DateTime CreatedAt,
    string? AuthProvider);

public record UserDetailDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTime? LastLogin,
    DateTime CreatedAt,
    string? AuthProvider,
    int TotalVocabularies,
    int TotalReviews,
    DateTime? PlanExpirationDate,
    IReadOnlyList<AdminSubscriptionDto> Subscriptions);

public record AdminSubscriptionDto(
    Guid Id,
    string Plan,
    string Status,
    DateTime StartDate,
    DateTime? EndDate,
    string Provider,
    string? ExternalSubscriptionId);

public record UpdateUserRoleRequest(string Role);
public record UpdateUserStatusRequest(bool IsActive);
public record AddSubscriptionRequest(string Plan, int DurationDays);

public record SystemStatsDto(
    int TotalUsers,
    int TotalPremiumUsers,
    int TotalVocabularies,
    int TotalReviews,
    int TotalActiveSubscriptions);
