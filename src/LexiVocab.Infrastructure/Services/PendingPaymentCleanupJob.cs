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

    public PendingPaymentCleanupJob(AppDbContext db, IConfiguration config, ILogger<PendingPaymentCleanupJob> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var candidates = await _db.PaymentTransactions
            .Include(t => t.Subscription)
            .Where(t =>
                t.Status == PaymentStatus.Pending &&
                (t.ExpiresAt != null && t.ExpiresAt <= now))
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        foreach (var tx in candidates)
        {
            tx.Status = PaymentStatus.Expired;
            tx.CancelledAt = now;
            tx.CancelReason = "Expired by configured pending payment expiry.";

            if (tx.Subscription != null && tx.Subscription.Status == SubscriptionStatus.Pending)
                tx.Subscription.Status = SubscriptionStatus.Cancelled;
        }

        var changed = await _db.SaveChangesAsync(ct);
        _logger.LogInformation("PendingPaymentCleanupJob: updated {Count} tx(s), SaveChanges={Changed}", candidates.Count, changed);
    }
}

