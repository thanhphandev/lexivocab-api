using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Hangfire recurring job that checks for expired subscriptions,
/// sends expiry warnings, and reverts user roles back to 'User'.
/// </summary>
public class SubscriptionExpirationJob : ISubscriptionExpirationJob
{
    private readonly AppDbContext _db;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SubscriptionExpirationJob> _logger;

    public SubscriptionExpirationJob(
        AppDbContext db,
        IEmailQueueService emailQueue,
        IEmailTemplateService templateService,
        IConfiguration configuration,
        ILogger<SubscriptionExpirationJob> logger)
    {
        _db = db;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Subscription Expiration Hangfire Job is starting.");

        try
        {
            await ProcessExpirationsAsync(ct);
            await SendExpiryWarningsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred executing Subscription Expiration Job.");
            throw; // Rethrow to let Hangfire mark it as failed & retry
        }

        _logger.LogInformation("Subscription Expiration Hangfire Job completed.");
    }

    private async Task ProcessExpirationsAsync(CancellationToken ct)
    {
        var appUrl = _configuration["App:Url"] ?? "https://lexivocab.store";

        var now = DateTime.UtcNow;

        var expiredUsers = await _db.Users
            .Where(u => u.Role == UserRole.Premium && 
                        u.PlanExpirationDate.HasValue && 
                        u.PlanExpirationDate.Value < now)
            .ToListAsync(ct);

        if (expiredUsers.Count > 0)
        {
            _logger.LogInformation("Found {Count} users with expired subscriptions. Reverting to Free tier.", expiredUsers.Count);

            foreach (var user in expiredUsers)
            {
                user.Role = UserRole.User;

                // Send expired notification
                try
                {
                    var html = await _templateService.RenderTemplateAsync("SubscriptionExpired", new Dictionary<string, string>
                    {
                        { "FullName", user.FullName },
                        { "AppUrl", appUrl }
                    });
                    _emailQueue.EnqueueEmail(user.Email, "📋 Your Premium Has Expired", html);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send expiration email to {Email}", user.Email);
                }
            }

            var userIds = expiredUsers.Select(u => u.Id).ToList();
            var activeSubscriptions = await _db.Subscriptions
                .Where(s => userIds.Contains(s.UserId) && s.Status == SubscriptionStatus.Active)
                .ToListAsync(ct);

            foreach (var sub in activeSubscriptions)
            {
                if (sub.EndDate.HasValue && sub.EndDate.Value < now)
                {
                    sub.Status = SubscriptionStatus.Expired;
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully completed subscription expiration processing.");
        }
    }

    private async Task SendExpiryWarningsAsync(CancellationToken ct)
    {
        var appUrl = _configuration["App:Url"] ?? "https://lexivocab.store";

        var now = DateTime.UtcNow;
        var threeDaysLater = now.AddDays(3);

        // Find premium users expiring in the next 3 days (but not already expired)
        var expiringUsers = await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Premium &&
                        u.PlanExpirationDate.HasValue &&
                        u.PlanExpirationDate.Value > now &&
                        u.PlanExpirationDate.Value <= threeDaysLater)
            .ToListAsync(ct);

        if (expiringUsers.Count > 0)
        {
            _logger.LogInformation("Sending expiry warnings to {Count} users.", expiringUsers.Count);

            foreach (var user in expiringUsers)
            {
                try
                {
                    var html = await _templateService.RenderTemplateAsync("SubscriptionExpiring", new Dictionary<string, string>
                    {
                        { "FullName", user.FullName },
                        { "ExpiryDate", user.PlanExpirationDate!.Value.ToString("MMMM dd, yyyy") },
                        { "AppUrl", appUrl }
                    });
                    _emailQueue.EnqueueEmail(user.Email, "⏰ Your Premium is Expiring Soon", html);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send expiry warning to {Email}", user.Email);
                }
            }
        }
    }
}

