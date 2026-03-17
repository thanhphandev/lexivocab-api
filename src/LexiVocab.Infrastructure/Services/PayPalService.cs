using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// PayPal REST API v2 Implementation.
/// Uses raw HttpClient to avoid heavy SDK dependencies and provide full control.
/// </summary>
public class PayPalService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly IUnitOfWork _uow;
    private readonly IPricingCalculator _pricingCalculator;
    private readonly ILogger<PayPalService> _logger;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _webhookId;
    private readonly string _returnUrl;
    private readonly string _cancelUrl;
    private readonly bool _isProduction;
    private readonly int _pendingPaymentExpiresInMinutes;

    private const string CurrencyCode = "USD";

    public PaymentProvider Provider => PaymentProvider.PayPal;

    public PayPalService(
        HttpClient httpClient,
        IUnitOfWork uow,
        IPricingCalculator pricingCalculator,
        IConfiguration config,
        IHostEnvironment env,
        ILogger<PayPalService> logger,
        IEmailQueueService emailQueue,
        IEmailTemplateService templateService)
    {
        _httpClient = httpClient;
        _uow = uow;
        _pricingCalculator = pricingCalculator;
        _logger = logger;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _isProduction = env.IsProduction();

        _pendingPaymentExpiresInMinutes = config.GetValue<int?>("Payments:PendingPaymentExpiresInMinutes") ?? 10;
        _clientId = config["PayPal:ClientId"] ?? "";
        _clientSecret = config["PayPal:ClientSecret"] ?? "";
        _webhookId = config["PayPal:WebhookId"] ?? "";
        _returnUrl = config["PayPal:ReturnUrl"] ?? "http://localhost:3000/checkout/success";
        _cancelUrl = config["PayPal:CancelUrl"] ?? "http://localhost:3000/pricing";

        var baseUrl = config["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString() ?? "";
    }

    public async Task<string> CreateOrderAsync(Guid userId, string planId, int durationMonths, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);

        if (!Guid.TryParse(planId, out var planGuid))
            throw new ArgumentException("Invalid subscription plan ID.");

        var plan = await _uow.PlanDefinitions.GetByIdAsync(planGuid, ct)
                   ?? throw new ArgumentException("Subscription plan not found.");

        // Calculate dynamic price with discount
        var pricing = _pricingCalculator.CalculatePrice(plan.Price, durationMonths);
        var amount = pricing.FinalPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var description = $"LexiVocab {plan.Name} - {durationMonths} month(s) (Save {pricing.DiscountPercent:F0}%)";

        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = userId.ToString(),
                    amount = new
                    {
                        currency_code = CurrencyCode,
                        value = amount
                    },
                    description = description
                }
            },
            application_context = new
            {
                user_action = "PAY_NOW",
                return_url = _returnUrl,
                cancel_url = _cancelUrl
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/v2/checkout/orders")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct);

        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal Create Order Failed: {Json}", json);
            throw new InvalidOperationException("Failed to create PayPal order.");
        }

        using var doc = JsonDocument.Parse(json);
        var links = doc.RootElement.GetProperty("links").EnumerateArray();

        foreach (var link in links)
        {
            if (link.GetProperty("rel").GetString() == "approve")
            {
                var orderId = doc.RootElement.GetProperty("id").GetString()!;

                // Create Subscription as PENDING — will be activated only after capture
                var sub = new Subscription
                {
                    UserId = userId,
                    PlanDefinitionId = plan.Id,
                    Status = SubscriptionStatus.Pending,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(durationMonths),
                    Provider = PaymentProvider.PayPal,
                    DurationMonths = durationMonths // Store the duration
                };

                var tx = new PaymentTransaction
                {
                    UserId = userId,
                    Subscription = sub,
                    Provider = PaymentProvider.PayPal,
                    ExternalOrderId = orderId,
                    Amount = pricing.FinalPrice,
                    Currency = CurrencyCode,
                    Status = PaymentStatus.Pending,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_pendingPaymentExpiresInMinutes)
                };

                _uow.Subscriptions.Add(sub);
                _uow.PaymentTransactions.Add(tx);
                await _uow.SaveChangesAsync(ct);

                return link.GetProperty("href").GetString()!;
            }
        }

        throw new InvalidOperationException("Approval URL not found in PayPal response.");
    }

    public async Task<bool> CaptureOrderAsync(string orderId, Guid userId, CancellationToken ct)
    {
        // 1. Quick check: Is it already completed in our system?
        var tx = await _uow.PaymentTransactions.GetByExternalOrderIdAsync(orderId, ct);
        if (tx is { Status: PaymentStatus.Completed })
        {
            _logger.LogInformation("PayPal Capture: Order {OrderId} already marked as COMPLETED in DB. Skipping API call.", orderId);
            return true;
        }

        var token = await GetAccessTokenAsync(ct);
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/checkout/orders/{orderId}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Required for capture endpoints
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json"); 

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        
        if (!response.IsSuccessStatusCode)
        {
            // Handle idempotency from PayPal's side
            if (json.Contains("ORDER_ALREADY_CAPTURED"))
            {
                _logger.LogInformation("PayPal Capture: Order {OrderId} was already captured on PayPal side. Activating locally.", orderId);
                await ActivateSubscriptionByOrderIdAsync(orderId, json, ct);
                return true;
            }

            _logger.LogError("PayPal Capture Failed for order {OrderId}: {Json}", orderId, json);
            return false;
        }

        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("status").GetString();

        if (status == "COMPLETED")
        {
            await ActivateSubscriptionByOrderIdAsync(orderId, json, ct);
            return true;
        }

        _logger.LogWarning("PayPal Capture returned status {Status} for order {OrderId}. Full JSON: {Json}", status, orderId, json);
        return false;
    }

    public async Task<bool> VerifyWebhookSignatureAsync(string body, IDictionary<string, string> headers)
    {
        // In production, webhook verification is mandatory
        if (string.IsNullOrEmpty(_webhookId))
        {
            if (_isProduction)
            {
                _logger.LogError("PayPal Webhook ID is not configured in production. Rejecting webhook.");
                return false;
            }
            
            _logger.LogWarning("PayPal Webhook ID is not configured. Trusting webhook by default (Development only).");
            return true;
        }

        try 
        {
            var token = await GetAccessTokenAsync(CancellationToken.None);

            var authAlgo = headers.FirstOrDefault(x => x.Key.Equals("PAYPAL-AUTH-ALGO", StringComparison.OrdinalIgnoreCase)).Value;
            var certUrl = headers.FirstOrDefault(x => x.Key.Equals("PAYPAL-CERT-URL", StringComparison.OrdinalIgnoreCase)).Value;
            var transmissionId = headers.FirstOrDefault(x => x.Key.Equals("PAYPAL-TRANSMISSION-ID", StringComparison.OrdinalIgnoreCase)).Value;
            var transmissionSig = headers.FirstOrDefault(x => x.Key.Equals("PAYPAL-TRANSMISSION-SIG", StringComparison.OrdinalIgnoreCase)).Value;
            var transmissionTime = headers.FirstOrDefault(x => x.Key.Equals("PAYPAL-TRANSMISSION-TIME", StringComparison.OrdinalIgnoreCase)).Value;

            if (string.IsNullOrEmpty(authAlgo) || string.IsNullOrEmpty(transmissionSig))
            {
                _logger.LogWarning("Missing required PayPal headers for signature verification.");
                return false;
            }

            var verifyPayload = new
            {
                auth_algo = authAlgo,
                cert_url = certUrl,
                transmission_id = transmissionId,
                transmission_sig = transmissionSig,
                transmission_time = transmissionTime,
                webhook_id = _webhookId,
                webhook_event = JsonSerializer.Deserialize<object>(body)
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/v1/notifications/verify-webhook-signature")
            {
                Content = new StringContent(JsonSerializer.Serialize(verifyPayload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("PayPal webhook verification request failed: {StatusCode} - {Body}", response.StatusCode, errorBody);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var verifyStatus = doc.RootElement.GetProperty("verification_status").GetString();

            return verifyStatus == "SUCCESS";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PayPal webhook signature.");
            return false;
        }
    }

    public async Task ProcessWebhookEventAsync(string body, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(body);
        var eventType = doc.RootElement.GetProperty("event_type").GetString();

        _logger.LogInformation("Processing PayPal webhook event: {EventType}", eventType);

        switch (eventType)
        {
            case "PAYMENT.CAPTURE.COMPLETED":
                await HandlePaymentCaptureCompletedAsync(doc.RootElement, ct);
                break;

            case "PAYMENT.CAPTURE.REFUNDED":
                await HandlePaymentRefundedAsync(doc.RootElement, ct);
                break;

            case "PAYMENT.CAPTURE.DENIED":
            case "PAYMENT.CAPTURE.DECLINED":
                await HandlePaymentFailedAsync(doc.RootElement, ct);
                break;

            default:
                _logger.LogInformation("Unhandled PayPal webhook event type: {EventType}", eventType);
                break;
        }
    }

    // ─── Webhook Event Handlers ─────────────────────────────────────

    private async Task HandlePaymentCaptureCompletedAsync(JsonElement root, CancellationToken ct)
    {
        try
        {
            var resource = root.GetProperty("resource");
            
            // The supplementary_data contains the order_id
            string? orderId = null;
            if (resource.TryGetProperty("supplementary_data", out var suppData) &&
                suppData.TryGetProperty("related_ids", out var relatedIds) &&
                relatedIds.TryGetProperty("order_id", out var orderIdElement))
            {
                orderId = orderIdElement.GetString();
            }

            // Fallback: try to get from resource.id (capture id) and look up by it
            if (string.IsNullOrEmpty(orderId))
            {
                _logger.LogWarning("Could not extract order_id from PAYMENT.CAPTURE.COMPLETED webhook. Skipping.");
                return;
            }

            await ActivateSubscriptionByOrderIdAsync(orderId, root.GetRawText(), ct);
            _logger.LogInformation("Successfully processed PAYMENT.CAPTURE.COMPLETED for order {OrderId}", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PAYMENT.CAPTURE.COMPLETED webhook");
        }
    }

    private async Task HandlePaymentRefundedAsync(JsonElement root, CancellationToken ct)
    {
        try
        {
            var resource = root.GetProperty("resource");
            string? orderId = null;

            if (resource.TryGetProperty("supplementary_data", out var suppData) &&
                suppData.TryGetProperty("related_ids", out var relatedIds) &&
                relatedIds.TryGetProperty("order_id", out var orderIdElement))
            {
                orderId = orderIdElement.GetString();
            }

            if (string.IsNullOrEmpty(orderId))
            {
                _logger.LogWarning("Could not extract order_id from PAYMENT.CAPTURE.REFUNDED webhook. Skipping.");
                return;
            }

            var tx = await _uow.PaymentTransactions.GetByExternalOrderIdWithDetailsAsync(orderId, ct);

            if (tx == null)
            {
                _logger.LogWarning("PaymentTransaction not found for refunded order {OrderId}", orderId);
                return;
            }

            tx.Status = PaymentStatus.Refunded;
            tx.Subscription.Status = SubscriptionStatus.Cancelled;

            await _uow.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully processed refund for order {OrderId}, user {UserId} downgraded to Free",
                orderId, tx.UserId);

            // Send refund email
            try
            {
                var user = tx.Subscription.User;
                var html = await _templateService.RenderTemplateAsync("PaymentRefunded", new Dictionary<string, string>
                {
                    { "FullName", user.FullName },
                    { "Amount", $"${tx.Amount:F2} {tx.Currency}" }
                });
                _emailQueue.EnqueueEmail(user.Email, "💰 Payment Refunded", html);
            }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "Failed to send refund email for order {OrderId}", orderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PAYMENT.CAPTURE.REFUNDED webhook");
        }
    }

    private async Task HandlePaymentFailedAsync(JsonElement root, CancellationToken ct)
    {
        try
        {
            var resource = root.GetProperty("resource");
            string? orderId = null;

            if (resource.TryGetProperty("supplementary_data", out var suppData) &&
                suppData.TryGetProperty("related_ids", out var relatedIds) &&
                relatedIds.TryGetProperty("order_id", out var orderIdElement))
            {
                orderId = orderIdElement.GetString();
            }

            if (string.IsNullOrEmpty(orderId))
            {
                _logger.LogWarning("Could not extract order_id from payment failed webhook. Skipping.");
                return;
            }

            var tx = await _uow.PaymentTransactions.GetByExternalOrderIdWithDetailsAsync(orderId, ct);

            if (tx == null) return;

            tx.Status = PaymentStatus.Failed;
            tx.Subscription.Status = SubscriptionStatus.Cancelled;

            await _uow.SaveChangesAsync(ct);
            _logger.LogInformation("Marked payment as failed for order {OrderId}", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment failed webhook");
        }
    }

    // ─── Shared Logic ────────────────────────────────────────────────

    /// <summary>
    /// Activates a subscription by its PayPal order ID. Used by both CaptureOrderAsync and webhook handler.
    /// Idempotent: skips if already completed.
    /// </summary>
    private async Task ActivateSubscriptionByOrderIdAsync(string orderId, string rawPayload, CancellationToken ct)
    {
        // Use a lock-like check via status update to prevent double activation
        var tx = await _uow.PaymentTransactions.GetByExternalOrderIdWithDetailsAsync(orderId, ct);

        if (tx == null)
        {
            _logger.LogWarning("ActivateSubscription: PaymentTransaction not found for order {OrderId}", orderId);
            return;
        }

        // Idempotent check
        if (tx.Status == PaymentStatus.Completed)
        {
            _logger.LogInformation("ActivateSubscription: Order {OrderId} already COMPLETED. Skipping activation.", orderId);
            return;
        }

        _logger.LogInformation("Activating subscription for Order {OrderId}, User {UserId}", orderId, tx.UserId);

        tx.Status = PaymentStatus.Completed;
        tx.PaidAt = DateTime.UtcNow;
        tx.RawPayload = rawPayload;
        
        // Activate the subscription (was Pending)
        tx.Subscription.Status = SubscriptionStatus.Active;
        // Re-calculate dates to ensure plan duration is respected starting from payment
        var now = DateTime.UtcNow;
        var oldStartDate = tx.Subscription.StartDate;
        tx.Subscription.StartDate = now;
        
        if (tx.Subscription.EndDate.HasValue)
        {
            // Get original intended duration (e.g. 30 days)
            var duration = tx.Subscription.EndDate.Value - oldStartDate;
            tx.Subscription.EndDate = now + duration;
        }

        await _uow.SaveChangesAsync(ct);

        // Send payment success email
        try
        {
            var user = tx.Subscription.User;
            var planName = tx.Subscription.PlanDefinition?.Name ?? "Premium";
            
            _logger.LogInformation("Sending success email to {Email} for plan {PlanName}", user.Email, planName);

            var html = await _templateService.RenderTemplateAsync("PaymentSuccess", new Dictionary<string, string>
            {
                { "FullName", user.FullName },
                { "PlanName", planName },
                { "Amount", $"${tx.Amount:F2} {tx.Currency}" },
                { "ExpiryDate", tx.Subscription.EndDate?.ToString("MMMM dd, yyyy") ?? "Lifetime" },
                { "TransactionId", tx.ExternalOrderId ?? tx.Id.ToString() }
            });
            _emailQueue.EnqueueEmail(user.Email, "✅ Payment Successful!", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment success email for order {OrderId}", orderId);
        }
    }

    public string? GetApprovalUrl(string reference, decimal amount)
    {
        return null; // PayPal URLs are short-lived and dynamic
    }
}
