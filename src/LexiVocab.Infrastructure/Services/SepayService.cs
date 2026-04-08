using System.Text.Json;
using System.Text.RegularExpressions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Sepay (Vietnam) Payment Integration.
/// Focuses on Bank Transfer automation via VietQR and Webhooks.
/// </summary>
public class SepayService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SepayService> _logger;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly int _pendingPaymentExpiresInMinutes;
    private readonly string _apiKey;
    private readonly string _apiBaseUrl;
    private readonly string _qrTemplate;
    private readonly string _bankAccount;
    private readonly string _bankName;

    public PaymentProvider Provider => PaymentProvider.Sepay;

    public SepayService(
        IUnitOfWork uow,
        IConfiguration config,
        ILogger<SepayService> logger,
        IEmailQueueService emailQueue,
        IEmailTemplateService templateService)
    {
        _uow = uow;
        _logger = logger;
        _emailQueue = emailQueue;
        _templateService = templateService;
        
        _pendingPaymentExpiresInMinutes = config.GetValue<int?>("Payments:PendingPaymentExpiresInMinutes") ?? 10;
        _apiKey = config["Sepay:ApiKey"] ?? "";
        _apiBaseUrl = config["Sepay:ApiBaseUrl"] ?? "https://my.sepay.vn/api";
        _qrTemplate = config["Sepay:QrTemplate"] ?? "https://qr.sepay.vn/img?acc={0}&bank={1}&amount={2}&des={3}";
        _bankAccount = config["Sepay:BankAccount"] ?? "";
        _bankName = config["Sepay:BankName"] ?? "";
    }

    public async Task<string> CreateOrderAsync(Guid userId, string pricingId, string? couponCode = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_bankAccount) || string.IsNullOrEmpty(_bankName))
        {
            _logger.LogError("Sepay configuration missing: BankAccount='{BankAccount}', BankName='{BankName}'", _bankAccount, _bankName);
            throw new InvalidOperationException("Sepay payment is not properly configured. Please contact support.");
        }

        if (!Guid.TryParse(pricingId, out var pricingGuid))
            throw new ArgumentException("Invalid subscription pricing ID.");

        var pricing = await _uow.PlanPricings.GetByIdWithPlanAsync(pricingGuid, ct)
                   ?? throw new ArgumentException("Subscription pricing not found.");
        var plan = pricing.Plan;

        Coupon? coupon = null;
        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var code = couponCode.Trim().ToUpperInvariant();
            coupon = await _uow.Coupons.GetByCodeAsync(code, ct);
            if (coupon == null || !coupon.IsActive) throw new InvalidOperationException("Invalid or inactive coupon code.");
            if (coupon.ValidFrom.HasValue && coupon.ValidFrom > DateTime.UtcNow) throw new InvalidOperationException("Coupon is not yet valid.");
            if (coupon.ValidUntil.HasValue && coupon.ValidUntil < DateTime.UtcNow) throw new InvalidOperationException("Coupon has expired.");
            if (coupon.MaxUses.HasValue && coupon.CurrentUses >= coupon.MaxUses) throw new InvalidOperationException("Coupon usage limit reached.");
        }

        var finalPrice = pricing.Price;
        if (coupon != null)
        {
            if (coupon.DiscountType == DiscountType.Percentage)
            {
                finalPrice -= finalPrice * (coupon.DiscountValue / 100m);
            }
            else
            {
                if (!string.Equals(coupon.Currency, pricing.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"This fixed-amount coupon is for {coupon.Currency?.ToUpper()} and cannot be used with a {pricing.Currency.ToUpper()} plan.");
                }
                finalPrice -= coupon.DiscountValue;
            }
            if (finalPrice < 0) finalPrice = 0;
        }

        // Create a unique reference for the bank transfer description
        // Format: LV [UserId Short] [ShortGuid]
        var reference = $"LV{userId.ToString()[..4]}{Guid.NewGuid().ToString()[..4]}".ToUpper();

        var sub = new Subscription
        {
            UserId = userId,
            PlanDefinitionId = plan.Id,
            PlanPricingId = pricing.Id,
            Status = SubscriptionStatus.Pending,
            StartDate = DateTime.UtcNow,
            EndDate = pricing.DurationDays.HasValue ? DateTime.UtcNow.AddDays(pricing.DurationDays.Value) : null,
            Provider = PaymentProvider.Sepay,
            ExternalSubscriptionId = reference
        };

        var tx = new PaymentTransaction
        {
            UserId = userId,
            Subscription = sub,
            Provider = PaymentProvider.Sepay,
            ExternalOrderId = reference,
            Amount = finalPrice,
            Currency = pricing.Currency,
            Status = PaymentStatus.Pending,
            CouponId = coupon?.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_pendingPaymentExpiresInMinutes)
        };

        _uow.Subscriptions.Add(sub);
        _uow.PaymentTransactions.Add(tx);
        await _uow.SaveChangesAsync(ct);

        // For Sepay, the "Approval URL" is actually a page showing the QR or a direct VietQR link
        var qrUrl = string.Format(_qrTemplate, _bankAccount, _bankName, (int)finalPrice, reference);
        
        return qrUrl;
    }

    public Task<bool> CaptureOrderAsync(string orderId, Guid userId, CancellationToken ct)
    {
        // Sepay bank transfers are asynchronous via Webhook.
        return Task.FromResult(false); 
    }

    public Task<bool> VerifyWebhookSignatureAsync(string body, IDictionary<string, string> headers)
    {
        // Sepay sends an API Key in the headers.
        // Documentation: Checks 'Authorization' header or 'x-api-key'
        if (headers.TryGetValue("Authorization", out var authHeader))
        {
            var isValid = authHeader == $"Apikey {_apiKey}" || authHeader == $"Bearer {_apiKey}" || authHeader == _apiKey;
            if (!isValid) _logger.LogWarning("Sepay Webhook: Authorization header found but value mismatch.");
            return Task.FromResult(isValid);
        }
        
        if (headers.TryGetValue("x-api-key", out var apiKeyHeader))
        {
            var isValid = apiKeyHeader == _apiKey;
            if (!isValid) _logger.LogWarning("Sepay Webhook: x-api-key header found but value mismatch.");
            return Task.FromResult(isValid);
        }

        var keys = string.Join(", ", headers.Keys);
        _logger.LogWarning("Sepay Webhook: No Authorization or x-api-key header found. Available headers: {Keys}", keys);
        return Task.FromResult(false);
    }

    public async Task ProcessWebhookEventAsync(string body, CancellationToken ct)
    {
        _logger.LogInformation("Processing Sepay webhook: {Body}", body);
        
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        
        // Sepay fields: 'content' or 'description' usually contains the reference
        string rawContent = "";
        if (root.TryGetProperty("content", out var contentElement))
            rawContent = contentElement.GetString() ?? "";
        else if (root.TryGetProperty("description", out var descElement))
            rawContent = descElement.GetString() ?? "";
        else if (root.TryGetProperty("transaction_content", out var legacyElement)) // Fallback to legacy if any
            rawContent = legacyElement.GetString() ?? "";

        if (string.IsNullOrEmpty(rawContent))
        {
            _logger.LogWarning("Sepay webhook missing reference content.");
            return;
        }

        // Use Regex to extract the reference (LV + 8 chars)
        var match = Regex.Match(rawContent, @"LV[A-Z0-9]{8}", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            _logger.LogWarning("Sepay webhook content does not contain a valid reference: {Content}", rawContent);
            return;
        }

        var reference = match.Value.ToUpper();

        var tx = await _uow.PaymentTransactions
            .GetByExternalOrderIdAsync(reference, ct);

        if (tx == null)
        {
            _logger.LogWarning("Sepay transaction not found for reference: {Reference}", reference);
            return;
        }

        if (tx.IsTerminal)
        {
            _logger.LogInformation("Sepay transaction {Reference} already in terminal state {Status}.", reference, tx.Status);
            return;
        }

        // 1. Strict Amount Check (Sepay uses 'transferAmount')
        decimal transferAmount = 0;
        if (root.TryGetProperty("transferAmount", out var amountElement))
        {
            transferAmount = amountElement.GetDecimal();
        }
        else if (root.TryGetProperty("amount_in", out var altAmountElement))
        {
            transferAmount = altAmountElement.GetDecimal();
        }

        if (transferAmount < tx.Amount)
        {
            _logger.LogWarning("Sepay amount mismatch. Expected at least: {Expected}, Received: {Received}. Reference: {Reference}", tx.Amount, transferAmount, reference);
            return;
        }

        // 2. Idempotency Check (Using Sepay 'id')
        var bankTransactionId = root.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;
        if (!string.IsNullOrEmpty(bankTransactionId))
        {
            var exists = await _uow.PaymentTransactions
                .ExistsByProviderResponseIdAsync(bankTransactionId, ct);
            if (exists)
            {
                _logger.LogInformation("Sepay transaction ID {BankTxId} already processed.", bankTransactionId);
                return;
            }
            tx.ProviderResponseId = bankTransactionId;
        }

        _logger.LogInformation("Activating Sepay subscription for reference {Reference}, User {UserId}", reference, tx.UserId);

        tx.Status = PaymentStatus.Completed;
        tx.PaidAt = DateTime.UtcNow;
        tx.RawPayload = body;
        
        // Ensure Subscription is loaded
        if (tx.Subscription == null)
        {
            _logger.LogInformation("Subscription not pre-loaded for transaction {Reference}. Attempting to load...", reference);
            tx.Subscription = (await _uow.Subscriptions.GetByIdAsync(tx.SubscriptionId, ct))!;
        }

        if (tx.Subscription == null)
        {
            _logger.LogError("Subscription {SubscriptionId} not found for transaction {Reference}. Cannot activate.", tx.SubscriptionId, reference);
            return; 
        }

        tx.Subscription.Status = SubscriptionStatus.Active;
        var now = DateTime.UtcNow;
        var oldStartDate = tx.Subscription.StartDate;
        tx.Subscription.StartDate = now;

        if (tx.Subscription.EndDate.HasValue)
        {
            var duration = tx.Subscription.EndDate.Value - oldStartDate;
            tx.Subscription.EndDate = now + duration;
        }

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique constraint") == true ||
                                           ex.InnerException?.Message.Contains("23505") == true)
        {
            // Unique constraint violation on ProviderResponseId - another webhook already processed this
            _logger.LogWarning("Duplicate Sepay webhook detected for transaction ID {BankTxId}. Another thread already processed this.", bankTransactionId);
            return;
        }

        _logger.LogInformation("Sepay payment completed successfully for reference {Reference}", reference);

        // Send Email
        try
        {
            var user = await _uow.Users.GetByIdAsync(tx.UserId, ct);
            if (user != null)
            {
                // Ensure PlanDefinition is loaded for the Name
                if (tx.Subscription.PlanDefinition == null)
                {
                    tx.Subscription.PlanDefinition = (await _uow.PlanDefinitions.GetByIdAsync(tx.Subscription.PlanDefinitionId, ct))!;
                }

                var html = await _templateService.RenderTemplateAsync("PaymentSuccess", new Dictionary<string, string>
                {
                    { "FullName", user.FullName },
                    { "PlanName", tx.Subscription.PlanDefinition?.Name ?? "Premium" },
                    { "CouponRow", tx.Coupon != null ? $"<tr><td style=\"padding-bottom: 12px; font-size: 14px; color: #6b7280;\">Discount ({tx.Coupon.Code})</td><td style=\"padding-bottom: 12px; font-size: 14px; color: #111827; text-align: right; font-weight: 600;\">{(tx.Coupon.DiscountType == DiscountType.Percentage ? $"-{tx.Coupon.DiscountValue}%" : $"-{tx.Coupon.DiscountValue:N0} {tx.Currency}")}</td></tr>" : "" },
                    { "Amount", $"{tx.Amount:N0} {tx.Currency}" },
                    { "ExpiryDate", tx.Subscription.EndDate?.ToString("MMMM dd, yyyy") ?? "Lifetime" },
                    { "TransactionId", reference }
                });
                _emailQueue.EnqueueEmail(user.Email, "✅ Payment Successful!", html);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Sepay success email.");
        }
    }

    public string? GetApprovalUrl(string reference, decimal amount)
    {
        if (string.IsNullOrEmpty(_bankAccount) || string.IsNullOrEmpty(_bankName))
            return null;

        return string.Format(_qrTemplate, _bankAccount, _bankName, (int)amount, reference);
    }

    public Task<LexiVocab.Application.Common.Result<bool>> RefundPaymentAsync(string externalTransactionId, string? reason = null, CancellationToken ct = default)
    {
        // Sepay (Bank Transfer) doesn't typically support API-based refunds.
        // It requires manual bank transfer. We just mark it in our system.
        return Task.FromResult(LexiVocab.Application.Common.Result<bool>.Success(true));
    }
}
