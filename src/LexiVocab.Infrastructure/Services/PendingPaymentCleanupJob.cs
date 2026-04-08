using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

public class PendingPaymentCleanupJob : IPendingPaymentCleanupJob
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<PendingPaymentCleanupJob> _logger;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;

    public PendingPaymentCleanupJob(
        AppDbContext db,
        IConfiguration config,
        ILogger<PendingPaymentCleanupJob> logger,
        IEmailQueueService emailQueue,
        IEmailTemplateService templateService)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _emailQueue = emailQueue;
        _templateService = templateService;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var candidates = await _db.PaymentTransactions
            .Include(t => t.Subscription)
            .Include(t => t.User)
            .Where(t =>
                t.Status == PaymentStatus.Pending &&
                (t.ExpiresAt != null && t.ExpiresAt <= now))
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        foreach (var tx in candidates)
        {
            // Use unified helper for core status updates
            var expired = LexiVocab.Application.Features.Payments.Queries.PaymentExpirationHelper.ExpireIfNeeded(tx, now, 
                (msg, args) => _logger.LogInformation(msg, args));

            if (expired)
            {
                // Send payment expired email
                try
                {
                    var user = tx.User;
                    if (user != null)
                    {
                        var html = await _templateService.RenderTemplateAsync("PaymentExpired", new Dictionary<string, string>
                        {
                            { "FullName", user.FullName },
                            { "Amount", $"{tx.Amount:F2} {tx.Currency}" }
                        });
                        _emailQueue.EnqueueEmail(user.Email, "⏰ Payment Expired", html);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send payment expired email for transaction {TransactionId}", tx.Id);
                }
            }
        }

        var changed = await _db.SaveChangesAsync(ct);
        _logger.LogInformation("PendingPaymentCleanupJob: checked {Count} tx(s), SaveChanges={Changed}", candidates.Count, changed);
    }
}

