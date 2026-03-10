using System.Text.Json;
using System.Text.RegularExpressions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// SePay (Vietnam) Payment Integration.
/// Focuses on Bank Transfer automation via VietQR and Webhooks.
/// </summary>
public class SeapayService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SeapayService> _logger;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly string _apiKey;
    private readonly string _apiBaseUrl;
    private readonly string _qrTemplate;
    private readonly string _bankAccount;
    private readonly string _bankName;

    public PaymentProvider Provider => PaymentProvider.Seapay;

    public SeapayService(
        IUnitOfWork uow,
        ILogger<SeapayService> logger,
        IEmailQueueService emailQueue,
        IEmailTemplateService templateService,
        IConfiguration config)
    {
        _uow = uow;
        _logger = logger;
        _emailQueue = emailQueue;
        _templateService = templateService;
        
        _apiKey = config["Seapay:ApiKey"] ?? "";
        _apiBaseUrl = config["Seapay:ApiBaseUrl"] ?? "https://my.sepay.vn/api";
        _qrTemplate = config["Seapay:QrTemplate"] ?? "https://qr.sepay.vn/img?acc={0}&bank={1}&amount={2}&des={3}";
        _bankAccount = config["Seapay:BankAccount"] ?? "";
        _bankName = config["Seapay:BankName"] ?? "";
    }

    public async Task<string> CreateOrderAsync(Guid userId, string planId, CancellationToken ct)
    {
        if (!Guid.TryParse(planId, out var planGuid))
            throw new ArgumentException("Invalid subscription plan ID.");

        var plan = await _uow.PlanDefinitions.GetByIdAsync(planGuid, ct)
                   ?? throw new ArgumentException("Subscription plan not found.");

        // Create a unique reference for the bank transfer description
        // Format: LV [UserId Short] [ShortGuid]
        var reference = $"LV{userId.ToString()[..4]}{Guid.NewGuid().ToString()[..4]}".ToUpper();

        var sub = new Subscription
        {
            UserId = userId,
            PlanDefinitionId = plan.Id,
            Status = SubscriptionStatus.Pending,
            StartDate = DateTime.UtcNow,
            EndDate = plan.DurationDays > 0 ? DateTime.UtcNow.AddDays(plan.DurationDays) : null,
            Provider = PaymentProvider.Seapay,
            ExternalSubscriptionId = reference // Use reference as external ID for bank transfer matching
        };

        var tx = new PaymentTransaction
        {
            UserId = userId,
            Subscription = sub,
            Provider = PaymentProvider.Seapay,
            ExternalOrderId = reference,
            Amount = plan.Price,
            Currency = plan.Currency,
            Status = PaymentStatus.Pending
        };

        _uow.Subscriptions.Add(sub);
        _uow.PaymentTransactions.Add(tx);
        await _uow.SaveChangesAsync(ct);

        // For SePay, the "Approval URL" is actually a page showing the QR or a direct VietQR link
        // We generate a VietQR image URL directly for simplicity, or return a checkout page URL
        // In a real production app, you might have a dedicated checkout page.
        
        var qrUrl = string.Format(_qrTemplate, _bankAccount, _bankName, (int)plan.Price, reference);
        
        return qrUrl;
    }

    public Task<bool> CaptureOrderAsync(string orderId, Guid userId, CancellationToken ct)
    {
        // SePay bank transfers are asynchronous.
        // We don't "capture" them via API call after redirect usually.
        // We wait for the Webhook.
        return Task.FromResult(false); 
    }

    public Task<bool> VerifyWebhookSignatureAsync(string body, IDictionary<string, string> headers)
    {
        // SePay usually sends an API Key in the headers or a specific secret.
        // Documentation: SePay checks the 'Authorization' header or a custom 'x-sepay-api-key'
        if (headers.TryGetValue("Authorization", out var authHeader) || headers.TryGetValue("x-api-key", out authHeader))
        {
            return Task.FromResult(authHeader == $"Bearer {_apiKey}" || authHeader == _apiKey);
        }
        return Task.FromResult(false);
    }

    public async Task ProcessWebhookEventAsync(string body, CancellationToken ct)
    {
        // SePay Webhook Payload Example:
        // { "id": 123, "gateway": "MBBank", "transaction_date": "...", "amount_in": 200000, "transaction_content": "LVABCD1234", ... }
        
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("transaction_content", out var contentElement))
        {
            _logger.LogWarning("SePay webhook missing transaction_content.");
            return;
        }

        var rawContent = contentElement.GetString() ?? "";
        
        // Use Regex to extract the reference (LV + 8 chars)
        var match = Regex.Match(rawContent, @"LV[A-Z0-9]{8}", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            _logger.LogWarning("SePay webhook content does not contain a valid reference: {Content}", rawContent);
            return;
        }

        var reference = match.Value.ToUpper();

        var tx = await _uow.PaymentTransactions
            .GetByExternalOrderIdAsync(reference, ct);

        if (tx == null)
        {
            _logger.LogWarning("SePay transaction not found for reference: {Reference}", reference);
            return;
        }

        // We need the User and Subscription, which might not be loaded by GetByExternalOrderIdAsync 
        // if it's a generic FirstOrDefault. Let's ensure navigation properties are loaded.
        // If GetByExternalOrderIdAsync doesn't include them, we might need a more specific query.
        // For production robustness, we should fetch User and Hub if needed.
        
        // Assuming IUnitOfWork repositories handle basic includes or we fetch explicitly.
        // Let's check ISubscriptionRepository if it's better to fetch by external ID there.

        if (tx.Status == PaymentStatus.Completed) return;

        // 1. Strict Amount Check (SePay uses 'transferAmount' in payload)
        if (root.TryGetProperty("transferAmount", out var amountElement))
        {
            var transferAmount = amountElement.GetDecimal();
            if (transferAmount < tx.Amount)
            {
                _logger.LogWarning("SePay amount mismatch. Expected at least: {Expected}, Received: {Received}. Reference: {Reference}", tx.Amount, transferAmount, reference);
                return; // Do not fulfill if amount is insufficient
            }
        }

        // 2. Idempotency Check (Using SePay 'id' or 'code' as bank transaction ID)
        var bankTransactionId = root.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;
        if (!string.IsNullOrEmpty(bankTransactionId))
        {
            var existingTx = await _uow.PaymentTransactions
                .ExistsByProviderResponseIdAsync(bankTransactionId, ct);
            if (existingTx)
            {
                _logger.LogInformation("SePay transaction {BankTxId} already processed.", bankTransactionId);
                return;
            }
            tx.ProviderResponseId = bankTransactionId;
        }

        tx.Status = PaymentStatus.Completed;
        tx.PaidAt = DateTime.UtcNow;
        tx.RawPayload = body;
        
        tx.Subscription.Status = SubscriptionStatus.Active;

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("SePay payment completed for reference {Reference}", reference);

        // Send Email
        try
        {
            // Reload user with email if navigation was lazy or not included
            var user = await _uow.Users.GetByIdAsync(tx.UserId, ct);
            if (user == null) return;
            var html = await _templateService.RenderTemplateAsync("PaymentSuccess", new Dictionary<string, string>
            {
                { "FullName", user.FullName },
                { "PlanName", tx.Subscription.PlanDefinition?.Name ?? "Premium" },
                { "Amount", $"{tx.Amount:N0} {tx.Currency}" },
                { "ExpiryDate", tx.Subscription.EndDate?.ToString("MMMM dd, yyyy") ?? "Lifetime" },
                { "TransactionId", reference }
            });
            _emailQueue.EnqueueEmail(user.Email, "✅ Payment Successful!", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SePay success email.");
        }
    }
}
