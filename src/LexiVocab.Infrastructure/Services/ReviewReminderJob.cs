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
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReviewReminderJob> _logger;

    public ReviewReminderJob(
        AppDbContext db,
        IEmailQueueService emailQueue,
        IEmailTemplateService templateService,
        IConfiguration configuration,
        ILogger<ReviewReminderJob> logger)
    {
        _db = db;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _configuration = configuration;
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
                    .Count(v => v.UserId == u.Id && !v.IsArchived && v.NextReviewDate <= now)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue review reminder for user {Email}", user.Email);
            }
        }

        _logger.LogInformation("Review reminders enqueued for {Count} users.", inactiveUsers.Count);
    }
}
