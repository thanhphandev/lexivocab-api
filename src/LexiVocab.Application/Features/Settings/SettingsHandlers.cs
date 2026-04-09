using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Settings;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Enums;
using MediatR;

namespace LexiVocab.Application.Features.Settings;

// ─── Get Settings ───────────────────────────────────────────────
public record GetSettingsQuery : IRequest<Result<UserSettingsDto>>;

public class GetSettingsHandler : IRequestHandler<GetSettingsQuery, Result<UserSettingsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEncryptionService _encryption;

    public GetSettingsHandler(IUnitOfWork uow, ICurrentUserService currentUser, IEncryptionService encryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _encryption = encryption;
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
                DefaultTranslator: "cloudflare",
                IsEmailReminderEnabled: true,
                IsTelegramReminderEnabled: false,
                TelegramBotToken: null,
                TelegramChatId: null,
                IsZaloReminderEnabled: false,
                ZaloBotToken: null,
                ZaloUserId: null));
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
            settings.DefaultTranslator,
            settings.IsEmailReminderEnabled,
            settings.IsTelegramReminderEnabled,
            _encryption.Decrypt(settings.TelegramBotToken ?? ""),
            settings.TelegramChatId,
            settings.IsZaloReminderEnabled,
            _encryption.Decrypt(settings.ZaloBotToken ?? ""),
            settings.ZaloUserId));
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
    string? DefaultTranslator,
    bool? IsEmailReminderEnabled,
    bool? IsTelegramReminderEnabled,
    string? TelegramBotToken,
    string? TelegramChatId,
    bool? IsZaloReminderEnabled,
    string? ZaloBotToken,
    string? ZaloUserId
) : IRequest<Result<UserSettingsDto>>;

public class UpdateSettingsHandler : IRequestHandler<UpdateSettingsCommand, Result<UserSettingsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEncryptionService _encryption;

    public UpdateSettingsHandler(IUnitOfWork uow, ICurrentUserService currentUser, IEncryptionService encryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _encryption = encryption;
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
        
        if (request.IsEmailReminderEnabled.HasValue) settings.IsEmailReminderEnabled = request.IsEmailReminderEnabled.Value;
        if (request.IsTelegramReminderEnabled.HasValue) settings.IsTelegramReminderEnabled = request.IsTelegramReminderEnabled.Value;
        if (request.TelegramBotToken is not null) 
            settings.TelegramBotToken = string.IsNullOrWhiteSpace(request.TelegramBotToken) 
                ? null : _encryption.Encrypt(request.TelegramBotToken);
        if (request.TelegramChatId is not null) settings.TelegramChatId = request.TelegramChatId;
        if (request.IsZaloReminderEnabled.HasValue) settings.IsZaloReminderEnabled = request.IsZaloReminderEnabled.Value;
        if (request.ZaloBotToken is not null) 
            settings.ZaloBotToken = string.IsNullOrWhiteSpace(request.ZaloBotToken) 
                ? null : _encryption.Encrypt(request.ZaloBotToken);
        if (request.ZaloUserId is not null) settings.ZaloUserId = request.ZaloUserId;
        
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
            settings.DefaultTranslator,
            settings.IsEmailReminderEnabled,
            settings.IsTelegramReminderEnabled,
            _encryption.Decrypt(settings.TelegramBotToken ?? ""),
            settings.TelegramChatId,
            settings.IsZaloReminderEnabled,
            _encryption.Decrypt(settings.ZaloBotToken ?? ""),
            settings.ZaloUserId));
    }
}

// ─── Test Bot Settings ───────────────────────────────────────────
public record TestBotSettingsCommand(
    string NativeLanguage,
    bool IsTelegramReminderEnabled,
    string TelegramBotToken,
    string TelegramChatId,
    bool IsZaloReminderEnabled,
    string ZaloBotToken,
    string ZaloUserId
) : IRequest<Result<bool>>;

public class TestBotSettingsHandler : IRequestHandler<TestBotSettingsCommand, Result<bool>>
{
    private readonly ITelegramNotificationService _telegramService;
    private readonly IZaloNotificationService _zaloService;

    public TestBotSettingsHandler(ITelegramNotificationService telegramService, IZaloNotificationService zaloService)
    {
        _telegramService = telegramService;
        _zaloService = zaloService;
    }

    public async Task<Result<bool>> Handle(TestBotSettingsCommand request, CancellationToken ct)
    {
        var lang = (request.NativeLanguage ?? "vi").ToLowerInvariant();
        var message = "✅ LexiVocab Bot Test: Test message confirming successful bot connection. Ready for automatic vocabulary reminders!";
        
        if (lang.StartsWith("vi"))
            message = "✅ LexiVocab Bot Test: Tin nhắn thử nghiệm xác nhận kết nối tự động thành công, sẵn sàng nhắc nhở từ vựng tới bạn!";
        else if (lang.StartsWith("ja"))
            message = "✅ LexiVocab Bot Test: ボットとの接続が正常に確認されました。語彙の学習リマインダーを確実に送信する準備ができました！";
        else if (lang.StartsWith("zh"))
            message = "✅ LexiVocab Bot Test: 机器人连接确认成功！词汇复习自动提醒功能已准备就绪。";
        
        bool telegramSuccess = true;
        bool zaloSuccess = true;
        
        if (request.IsTelegramReminderEnabled)
        {
            telegramSuccess = await _telegramService.SendMessageAsync(request.TelegramBotToken ?? "", request.TelegramChatId ?? "", message, ct);
            if (telegramSuccess) await _telegramService.SendStickerAsync(request.TelegramBotToken ?? "", request.TelegramChatId ?? "", "CAACAgIAAxkBAAPUab4o3K5cBSYEEpkbXT_dGcOnqXQAAtgPAAJI8mBLFfvE2nh0a5g6BA", ct);
        }
        
        if (request.IsZaloReminderEnabled)
        {
            zaloSuccess = await _zaloService.SendMessageAsync(request.ZaloBotToken ?? "", request.ZaloUserId ?? "", message, ct);
            if (zaloSuccess) await _zaloService.SendStickerAsync(request.ZaloBotToken ?? "", request.ZaloUserId ?? "", "f67c2c2c1069f937a078", ct);
        }
        
        if (!telegramSuccess || !zaloSuccess) 
            return Result<bool>.Failure("Failed to send test. Check token & ID validity.", 400, ErrorCode.VALIDATION_FAILED);

        return Result<bool>.Success(true);
    }
}
