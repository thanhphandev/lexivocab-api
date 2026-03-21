using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Settings;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Settings;

// ─── Get Settings ───────────────────────────────────────────────
public record GetSettingsQuery : IRequest<Result<UserSettingsDto>>;

public class GetSettingsHandler : IRequestHandler<GetSettingsQuery, Result<UserSettingsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetSettingsHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<UserSettingsDto>> Handle(GetSettingsQuery request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(_currentUser.UserId!.Value, ct);
        if (user is null)
            return Result<UserSettingsDto>.NotFound();

        var settings = user.UserSetting;
        if (settings is null)
        {
            // Return defaults if no settings record exists yet
            return Result<UserSettingsDto>.Success(new UserSettingsDto(
                IsHighlightEnabled: true,
                HighlightColor: "#FFD700",
                ExcludedDomains: [],
                DailyGoal: 20,
                DailyNewCardLimit: 20,
                DailyReviewLimit: 100,
                PreferencesJson: "{}",
                TargetLanguage: "English",
                NativeLanguage: "Vietnamese",
                CustomLlmsJson: "[]",
                DefaultTranslator: "cloudflare"));
        }

        return Result<UserSettingsDto>.Success(new UserSettingsDto(
            settings.IsHighlightEnabled,
            settings.HighlightColor,
            settings.ExcludedDomains,
            settings.DailyGoal,
            settings.DailyNewCardLimit,
            settings.DailyReviewLimit,
            settings.PreferencesJson,
            settings.TargetLanguage,
            settings.NativeLanguage,
            settings.CustomLlmsJson,
            settings.DefaultTranslator));
    }
}

// ─── Update Settings ────────────────────────────────────────────
public record UpdateSettingsCommand(
    bool? IsHighlightEnabled,
    string? HighlightColor,
    List<string>? ExcludedDomains,
    int? DailyGoal,
    int? DailyNewCardLimit,
    int? DailyReviewLimit,
    string? PreferencesJson,
    string? TargetLanguage,
    string? NativeLanguage,
    string? CustomLlmsJson,
    string? DefaultTranslator
) : IRequest<Result<UserSettingsDto>>;

public class UpdateSettingsHandler : IRequestHandler<UpdateSettingsCommand, Result<UserSettingsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateSettingsHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<UserSettingsDto>> Handle(UpdateSettingsCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<UserSettingsDto>.NotFound();

        var settings = user.UserSetting;
        if (settings is null)
        {
            // Create new settings if not exists (upsert semantics)
            settings = new UserSetting { UserId = userId };
            user.UserSetting = settings;
        }

        if (request.IsHighlightEnabled.HasValue) settings.IsHighlightEnabled = request.IsHighlightEnabled.Value;
        if (request.HighlightColor is not null) settings.HighlightColor = request.HighlightColor;
        if (request.ExcludedDomains is not null) settings.ExcludedDomains = request.ExcludedDomains;
        if (request.DailyGoal.HasValue) settings.DailyGoal = request.DailyGoal.Value;
        if (request.DailyNewCardLimit.HasValue) settings.DailyNewCardLimit = request.DailyNewCardLimit.Value;
        if (request.DailyReviewLimit.HasValue) settings.DailyReviewLimit = request.DailyReviewLimit.Value;
        if (request.PreferencesJson is not null) settings.PreferencesJson = request.PreferencesJson;
        if (request.TargetLanguage is not null) settings.TargetLanguage = request.TargetLanguage;
        if (request.NativeLanguage is not null) settings.NativeLanguage = request.NativeLanguage;
        if (request.CustomLlmsJson is not null) settings.CustomLlmsJson = request.CustomLlmsJson;
        if (request.DefaultTranslator is not null) settings.DefaultTranslator = request.DefaultTranslator;
        settings.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);

        return Result<UserSettingsDto>.Success(new UserSettingsDto(
            settings.IsHighlightEnabled,
            settings.HighlightColor,
            settings.ExcludedDomains,
            settings.DailyGoal,
            settings.DailyNewCardLimit,
            settings.DailyReviewLimit,
            settings.PreferencesJson,
            settings.TargetLanguage,
            settings.NativeLanguage,
            settings.CustomLlmsJson,
            settings.DefaultTranslator));
    }
}
