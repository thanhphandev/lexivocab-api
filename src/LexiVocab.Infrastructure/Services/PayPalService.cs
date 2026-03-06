using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Infrastructure.Persistence;
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
    private readonly AppDbContext _db;
    private readonly ILogger<PayPalService> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _webhookId;
    private readonly string _returnUrl;
    private readonly string _cancelUrl;
    private readonly bool _isProduction;

    private const string CurrencyCode = "USD";

    public PaymentProvider Provider => PaymentProvider.PayPal;

    public PayPalService(
        HttpClient httpClient,
        AppDbContext db,
        IConfiguration config,
        IHostEnvironment env,
        ILogger<PayPalService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _logger = logger;
        _isProduction = env.IsProduction();

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

    public async Task<string> CreateOrderAsync(Guid userId, string planId, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);

        var isYearly = planId.Contains("yearly", StringComparison.OrdinalIgnoreCase);
        var isMonthly = planId.Contains("monthly", StringComparison.OrdinalIgnoreCase);
        
        var amount = isYearly ? "99.99" : (isMonthly ? "9.99" : "199.99");
        var description = isYearly ? "LexiVocab Premium - 1 Year" : (isMonthly ? "LexiVocab Premium - 1 Month" : "LexiVocab Premium - Lifetime");

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
                    Plan = SubscriptionPlan.Premium,
                    Status = SubscriptionStatus.Pending,
                    StartDate = DateTime.UtcNow,
                    EndDate = isYearly ? DateTime.UtcNow.AddYears(1) : (isMonthly ? DateTime.UtcNow.AddMonths(1) : null),
                    Provider = PaymentProvider.PayPal
                };
                
                var tx = new PaymentTransaction
                {
                    UserId = userId,
                    Subscription = sub,
                    Provider = PaymentProvider.PayPal,
                    ExternalOrderId = orderId,
                    Amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
                    Currency = CurrencyCode,
                    Status = PaymentStatus.Pending
                };

                _db.Subscriptions.Add(sub);
                _db.PaymentTransactions.Add(tx);
                await _db.SaveChangesAsync(ct);

                return link.GetProperty("href").GetString()!;
            }
        }

        throw new InvalidOperationException("Approval URL not found in PayPal response.");
    }

    public async Task<bool> CaptureOrderAsync(string orderId, Guid userId, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/checkout/orders/{orderId}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Required for capture endpoints
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json"); 

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal Capture Failed: {Json}", json);
            return false;
        }

        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("status").GetString();

        if (status == "COMPLETED")
        {
            await ActivateSubscriptionByOrderIdAsync(orderId, json, ct);
            return true;
        }

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

            var tx = await _db.PaymentTransactions
                .Include(t => t.Subscription)
                .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(t => t.ExternalOrderId == orderId, ct);

            if (tx == null)
            {
                _logger.LogWarning("PaymentTransaction not found for refunded order {OrderId}", orderId);
                return;
            }

            tx.Status = PaymentStatus.Refunded;
            tx.Subscription.Status = SubscriptionStatus.Cancelled;
            tx.Subscription.User.Role = UserRole.User;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully processed refund for order {OrderId}, user {UserId} downgraded to Free",
                orderId, tx.UserId);
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

            var tx = await _db.PaymentTransactions
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.ExternalOrderId == orderId, ct);

            if (tx == null) return;

            tx.Status = PaymentStatus.Failed;
            tx.Subscription.Status = SubscriptionStatus.Cancelled;

            await _db.SaveChangesAsync(ct);
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
        var tx = await _db.PaymentTransactions
            .Include(t => t.Subscription)
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(t => t.ExternalOrderId == orderId, ct);

        if (tx == null)
        {
            _logger.LogWarning("PaymentTransaction not found for order {OrderId}", orderId);
            return;
        }

        // Idempotent — if already processed via Webhook or UI redirect
        if (tx.Status == PaymentStatus.Completed) return;

        tx.Status = PaymentStatus.Completed;
        tx.PaidAt = DateTime.UtcNow;
        tx.RawPayload = rawPayload;
        
        // Activate the subscription (was Pending)
        tx.Subscription.Status = SubscriptionStatus.Active;
        tx.Subscription.StartDate = DateTime.UtcNow;
        // Re-calculate EndDate from now (not from when order was created)
        if (tx.Subscription.EndDate.HasValue)
        {
            var duration = tx.Subscription.EndDate.Value - tx.Subscription.StartDate;
            tx.Subscription.EndDate = DateTime.UtcNow + duration;
        }
        
        // Upgrade User
        tx.Subscription.User.Role = UserRole.Premium;
        tx.Subscription.User.PlanExpirationDate = tx.Subscription.EndDate;

        await _db.SaveChangesAsync(ct);
    }
}
