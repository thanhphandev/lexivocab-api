using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Background job that runs periodically to check for expired subscriptions,
/// sends expiry warnings, and reverts user roles back to 'User'.
/// </summary>
public class SubscriptionExpirationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SubscriptionExpirationJob> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(12);

    public SubscriptionExpirationJob(IServiceProvider serviceProvider, ILogger<SubscriptionExpirationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscription Expiration Job is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpirationsAsync(stoppingToken);
                await SendExpiryWarningsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Subscription Expiration Job.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Subscription Expiration Job is stopping.");
    }

    private async Task ProcessExpirationsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailQueue = scope.ServiceProvider.GetRequiredService<IEmailQueueService>();
        var templateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();
        var appUrl = scope.ServiceProvider.GetRequiredService<IConfiguration>()["App:Url"] ?? "https://lexivocab.store";

        var now = DateTime.UtcNow;

        var expiredUsers = await db.Users
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
                    var html = await templateService.RenderTemplateAsync("SubscriptionExpired", new Dictionary<string, string>
                    {
                        { "FullName", user.FullName },
                        { "AppUrl", appUrl }
                    });
                    emailQueue.EnqueueEmail(user.Email, "📋 Your Premium Has Expired", html);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send expiration email to {Email}", user.Email);
                }
            }

            var userIds = expiredUsers.Select(u => u.Id).ToList();
            var activeSubscriptions = await db.Subscriptions
                .Where(s => userIds.Contains(s.UserId) && s.Status == SubscriptionStatus.Active)
                .ToListAsync(ct);

            foreach (var sub in activeSubscriptions)
            {
                if (sub.EndDate.HasValue && sub.EndDate.Value < now)
                {
                    sub.Status = SubscriptionStatus.Expired;
                }
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully completed subscription expiration processing.");
        }
    }

    /// <summary>
    /// Sends warning emails to users whose subscriptions expire within 3 days.
    /// </summary>
    private async Task SendExpiryWarningsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailQueue = scope.ServiceProvider.GetRequiredService<IEmailQueueService>();
        var templateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();
        var appUrl = scope.ServiceProvider.GetRequiredService<IConfiguration>()["App:Url"] ?? "https://lexivocab.store";

        var now = DateTime.UtcNow;
        var threeDaysLater = now.AddDays(3);

        // Find premium users expiring in the next 3 days (but not already expired)
        var expiringUsers = await db.Users
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
                    var html = await templateService.RenderTemplateAsync("SubscriptionExpiring", new Dictionary<string, string>
                    {
                        { "FullName", user.FullName },
                        { "ExpiryDate", user.PlanExpirationDate!.Value.ToString("MMMM dd, yyyy") },
                        { "AppUrl", appUrl }
                    });
                    emailQueue.EnqueueEmail(user.Email, "⏰ Your Premium is Expiring Soon", html);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send expiry warning to {Email}", user.Email);
                }
            }
        }
    }
}

