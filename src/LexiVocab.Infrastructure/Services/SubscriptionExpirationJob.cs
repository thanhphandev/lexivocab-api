using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
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
    private readonly IUnitOfWork _uow;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SubscriptionExpirationJob> _logger;

    public SubscriptionExpirationJob(
        IUnitOfWork uow,
        IEmailQueueService emailQueue,
        IEmailTemplateService templateService,
        IConfiguration configuration,
        ILogger<SubscriptionExpirationJob> logger)
    {
        _uow = uow;
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

        var expiredSubscriptions = await _uow.Subscriptions
            .GetExpiredWithUserAsync(now, ct);

        if (expiredSubscriptions.Count > 0)
        {
            _logger.LogInformation("Found {Count} expired subscriptions. Reverting to Free tier.", expiredSubscriptions.Count);

            foreach (var sub in expiredSubscriptions)
            {
                sub.Status = SubscriptionStatus.Expired;
                var user = sub.User;

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

            await _uow.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully completed subscription expiration processing.");
        }
    }

    private async Task SendExpiryWarningsAsync(CancellationToken ct)
    {
        var appUrl = _configuration["App:Url"] ?? "https://lexivocab.store";

        var now = DateTime.UtcNow;
        var threeDaysLater = now.AddDays(3);

        // Find active subscriptions expiring in the next 3 days
        var expiringSubscriptions = await _uow.Subscriptions
            .GetExpiringSoonWithUserAsync(now, threeDaysLater, ct);

        if (expiringSubscriptions.Count > 0)
        {
            _logger.LogInformation("Sending expiry warnings to {Count} subscriptions.", expiringSubscriptions.Count);

            foreach (var sub in expiringSubscriptions)
            {
                var user = sub.User;
                try
                {
                    var html = await _templateService.RenderTemplateAsync("SubscriptionExpiring", new Dictionary<string, string>
                    {
                        { "FullName", user.FullName },
                        { "ExpiryDate", sub.EndDate!.Value.ToString("MMMM dd, yyyy") },
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

