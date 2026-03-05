using LexiVocab.Domain.Enums;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Background job that runs periodically to check for expired subscriptions
/// and reverts user roles back to 'User'.
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Subscription Expiration Job.");
            }

            // Wait for the next cycle
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Subscription Expiration Job is stopping.");
    }

    private async Task ProcessExpirationsAsync(CancellationToken ct)
    {
        // Scope is required for DbContext in singleton BackgroundService
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Find users who have Premium role but their plan has expired
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
                user.Role = UserRole.User; // Back to free tier
                // Note: We keep PlanExpirationDate as is to track when it expired
            }

            // Also mark the overarching Subscription entities as Expired for UI consistency
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
}
