using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    
    // Hardcoded Premium Plan Price for demonstration
    private const string PremiumPrice = "9.99";
    private const string CurrencyCode = "USD";

    public PaymentProvider Provider => PaymentProvider.PayPal;

    public PayPalService(HttpClient httpClient, AppDbContext db, IConfiguration config, ILogger<PayPalService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _logger = logger;

        _clientId = config["PayPal:ClientId"] ?? "";
        _clientSecret = config["PayPal:ClientSecret"] ?? "";
        _webhookId = config["PayPal:WebhookId"] ?? "";
        
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
                        value = PremiumPrice
                    },
                    description = "LexiVocab Premium - Lifetime Upgrade"
                }
            },
            application_context = new
            {
                user_action = "PAY_NOW",
                return_url = "http://localhost:3000/checkout/success", // TODO: Move to config
                cancel_url = "http://localhost:3000/pricing"
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

                // Log the pending transaction to DB
                var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
                var sub = new Subscription
                {
                    UserId = userId,
                    Plan = SubscriptionPlan.Premium,
                    Status = SubscriptionStatus.Active, // Assuming lifetime for now
                    StartDate = DateTime.UtcNow,
                    Provider = PaymentProvider.PayPal
                };
                
                var tx = new PaymentTransaction
                {
                    UserId = userId,
                    Subscription = sub,
                    Provider = PaymentProvider.PayPal,
                    ExternalOrderId = orderId,
                    Amount = decimal.Parse(PremiumPrice),
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
            // Update Database Idempotently
            var tx = await _db.PaymentTransactions
                .Include(t => t.Subscription)
                .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(t => t.ExternalOrderId == orderId, ct);

            if (tx == null) return false;

            // If already processed via Webhook before UI redirect
            if (tx.Status == PaymentStatus.Completed) return true;

            tx.Status = PaymentStatus.Completed;
            tx.PaidAt = DateTime.UtcNow;
            tx.RawPayload = json;
            
            // Upgrade User
            tx.Subscription.User.Role = UserRole.Premium;
            // Empty expiration date = lifetime. If 1 year: .AddYears(1)
            tx.Subscription.User.PlanExpirationDate = null; 

            await _db.SaveChangesAsync(ct);
            return true;
        }

        return false;
    }

    public async Task<bool> VerifyWebhookSignatureAsync(string body, IDictionary<string, string> headers)
    {
        // For Sandbox testing, basic validation or true.
        // In production, implement full Verify Webhook Signature API call:
        // POST /v1/notifications/verify-webhook-signature
        return await Task.FromResult(true);
    }
}
