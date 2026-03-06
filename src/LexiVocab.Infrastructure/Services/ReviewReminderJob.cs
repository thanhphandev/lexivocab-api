using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Background job that runs daily to send review reminders to users
/// who haven't reviewed their vocabulary for 3+ days and have due cards.
/// </summary>
public class ReviewReminderJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReviewReminderJob> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public ReviewReminderJob(IServiceProvider serviceProvider, ILogger<ReviewReminderJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Review Reminder Job is starting.");

        // Wait 1 hour after startup before first check (avoid spamming on deploy)
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Review Reminder Job.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailQueue = scope.ServiceProvider.GetRequiredService<IEmailQueueService>();
        var templateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();
        var appUrl = scope.ServiceProvider.GetRequiredService<IConfiguration>()["App:Url"] ?? "https://lexivocab.store";

        var now = DateTime.UtcNow;
        var threeDaysAgo = now.AddDays(-3);

        // Find users who have vocabulary due for review AND haven't reviewed in 3+ days
        var inactiveUsers = await db.Users
            .AsNoTracking()
            .Where(u => db.UserVocabularies
                .Any(v => v.UserId == u.Id && !v.IsArchived && v.NextReviewDate <= now))
            .Where(u => !db.ReviewLogs
                .Any(r => r.UserId == u.Id && r.ReviewedAt >= threeDaysAgo))
            .Select(u => new
            {
                u.Email,
                u.FullName,
                LastReview = db.ReviewLogs
                    .Where(r => r.UserId == u.Id)
                    .OrderByDescending(r => r.ReviewedAt)
                    .Select(r => (DateTime?)r.ReviewedAt)
                    .FirstOrDefault(),
                DueCount = db.UserVocabularies
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

                var html = await templateService.RenderTemplateAsync("ReviewReminder", new Dictionary<string, string>
                {
                    { "FullName", user.FullName },
                    { "DueCount", user.DueCount.ToString() },
                    { "DaysMissed", daysMissed.ToString() },
                    { "LastReviewDate", lastReviewDate },
                    { "AppUrl", appUrl }
                });

                emailQueue.EnqueueEmail(user.Email, "📚 Your vocabulary cards are waiting!", html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue review reminder for user {Email}", user.Email);
            }
        }

        _logger.LogInformation("Review reminders enqueued for {Count} users.", inactiveUsers.Count);
    }
}
