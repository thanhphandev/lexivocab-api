using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Hangfire recurring job that runs daily to send review reminders to users
/// who haven't reviewed their vocabulary for 3+ days and have due cards.
/// </summary>
public class ReviewReminderJob : IReviewReminderJob
{
    private readonly AppDbContext _db;
    private readonly IEmailQueueService _emailQueue;
    private readonly ITelegramNotificationService _telegramService;
    private readonly IZaloNotificationService _zaloService;
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<ReviewReminderJob> _logger;

    public ReviewReminderJob(
        AppDbContext db,
        IEmailQueueService emailQueue,
        ITelegramNotificationService telegramService,
        IZaloNotificationService zaloService,
        IEmailTemplateService templateService,
        IConfiguration configuration,
        IEncryptionService encryption,
        ILogger<ReviewReminderJob> logger)
    {
        _db = db;
        _emailQueue = emailQueue;
        _telegramService = telegramService;
        _zaloService = zaloService;
        _templateService = templateService;
        _configuration = configuration;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Review Reminder Hangfire Job is starting.");

        try
        {
            await SendRemindersAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred executing Review Reminder Job.");
            throw; // Let Hangfire handle failure and retry
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        var appUrl = _configuration["App:Url"] ?? "https://lexivocab.store";

        var now = DateTime.UtcNow;
        var threeDaysAgo = now.AddDays(-3);

        // Find users who have vocabulary due for review AND haven't reviewed in 3+ days
        var inactiveUsers = await _db.Users
            .AsNoTracking()
            .Where(u => _db.UserVocabularies
                .Any(v => v.UserId == u.Id && !v.IsArchived && v.NextReviewDate <= now))
            .Where(u => !_db.ReviewLogs
                .Any(r => r.UserId == u.Id && r.ReviewedAt >= threeDaysAgo))
            .Select(u => new
            {
                u.Email,
                u.FullName,
                LastReview = _db.ReviewLogs
                    .Where(r => r.UserId == u.Id)
                    .OrderByDescending(r => r.ReviewedAt)
                    .Select(r => (DateTime?)r.ReviewedAt)
                    .FirstOrDefault(),
                DueCount = _db.UserVocabularies
                    .Count(v => v.UserId == u.Id && !v.IsArchived && v.NextReviewDate <= now),
                NativeLanguage = u.UserSetting != null ? u.UserSetting.NativeLanguage : "vi",
                IsEmailReminderEnabled = u.UserSetting == null || u.UserSetting.IsEmailReminderEnabled,
                IsTelegramReminderEnabled = u.UserSetting != null && u.UserSetting.IsTelegramReminderEnabled,
                TelegramBotToken = u.UserSetting != null ? u.UserSetting.TelegramBotToken : null,
                TelegramChatId = u.UserSetting != null ? u.UserSetting.TelegramChatId : null,
                IsZaloReminderEnabled = u.UserSetting != null && u.UserSetting.IsZaloReminderEnabled,
                ZaloBotToken = u.UserSetting != null ? u.UserSetting.ZaloBotToken : null,
                ZaloUserId = u.UserSetting != null ? u.UserSetting.ZaloUserId : null,
            })
            .ToListAsync(ct);

        if (inactiveUsers.Count == 0) return;

        _logger.LogInformation("Sending review reminders to {Count} inactive users.", inactiveUsers.Count);

        foreach (var user in inactiveUsers)
        {
            try
            {
                var lastReviewDate = user.LastReview?.ToString("MMM dd, yyyy") ?? "Never";
                var daysMissed = user.LastReview.HasValue
                    ? (int)(now - user.LastReview.Value).TotalDays
                    : 0;

                var textMessage = GetLocalizedReminderMessage(user.NativeLanguage, user.FullName, user.DueCount, daysMissed, appUrl);

                if (user.IsEmailReminderEnabled)
                {
                    var html = await _templateService.RenderTemplateAsync("ReviewReminder", new Dictionary<string, string>
                    {
                        { "FullName", user.FullName },
                        { "DueCount", user.DueCount.ToString() },
                        { "DaysMissed", daysMissed.ToString() },
                        { "LastReviewDate", lastReviewDate },
                        { "AppUrl", appUrl }
                    });

                    _emailQueue.EnqueueEmail(user.Email, "📚 Your vocabulary cards are waiting!", html);
                }

                if (user.IsTelegramReminderEnabled && !string.IsNullOrWhiteSpace(user.TelegramBotToken) && !string.IsNullOrWhiteSpace(user.TelegramChatId))
                {
                    var tgToken = _encryption.Decrypt(user.TelegramBotToken) ?? user.TelegramBotToken; // Fallback to plain if decryption fails (for transition)
                    await _telegramService.SendMessageAsync(tgToken, user.TelegramChatId, textMessage, ct);
                }

                if (user.IsZaloReminderEnabled && !string.IsNullOrWhiteSpace(user.ZaloBotToken) && !string.IsNullOrWhiteSpace(user.ZaloUserId))
                {
                    var zaToken = _encryption.Decrypt(user.ZaloBotToken) ?? user.ZaloBotToken;
                    await _zaloService.SendMessageAsync(zaToken, user.ZaloUserId, textMessage, ct);
                    // Send a lively sticker to Zalo via the SDK format!
                    await _zaloService.SendStickerAsync(zaToken, user.ZaloUserId, "f67c2c2c1069f937a078", ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue review reminder for user {Email}", user.Email);
            }
        }

        _logger.LogInformation("Review reminders enqueued for {Count} users.", inactiveUsers.Count);
    }

    private string GetLocalizedReminderMessage(string? nativeLang, string fullName, int dueCount, int daysMissed, string appUrl)
    {
        var lang = (nativeLang ?? "vi").ToLowerInvariant();
        if (lang.StartsWith("vi"))
            return $"📚 Chào {fullName}, bạn có {dueCount} từ vựng đang chờ ôn tập. Đã {daysMissed} ngày kể từ lần ôn tập cuối. Hãy tiếp tục cố gắng nhé!\n{appUrl}";
        if (lang.StartsWith("ja"))
            return $"📚 {fullName}さん、{dueCount}枚の単語カードが復習待ちです。前回の復習から{daysMissed}日経過しています。この調子で頑張りましょう！\n{appUrl}";
        if (lang.StartsWith("zh"))
            return $"📚 你好 {fullName}，你有 {dueCount} 个词汇卡片等待复习。距离上次复习已经过去 {daysMissed} 天。继续保持！\n{appUrl}";
        
        return $"📚 Hi {fullName}, you have {dueCount} vocabulary cards waiting for review. It's been {daysMissed} days since your last review. Keep up the momentum!\n{appUrl}";
    }
}
