using System.Text;
using System.Text.Json;
using LexiVocab.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services.Notifications;

public class ZaloNotificationService : IZaloNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZaloNotificationService> _logger;

    public ZaloNotificationService(HttpClient httpClient, ILogger<ZaloNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendMessageAsync(string botToken, string userId, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(userId))
            return false;

        var url = $"https://bot-api.zaloplatforms.com/bot{botToken}/sendMessage";
        
        var payload = new
        {
            chat_id = userId,
            text = message
        };
        
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content, ct);
            
            var responseString = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Zalo API HTTP error {StatusCode}: {ErrorResponse}", response.StatusCode, responseString);
                return false;
            }
            
            try 
            {
                using var doc = JsonDocument.Parse(responseString);
                if (doc.RootElement.TryGetProperty("ok", out var okElement) && !okElement.GetBoolean())
                {
                    _logger.LogWarning("Zalo API rejected the message: {ErrorResponse}", responseString);
                    return false;
                }
            } 
            catch { /* Not valid JSON, ignore */ }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Zalo message to UserId {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> SendStickerAsync(string botToken, string userId, string stickerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(stickerId))
            return false;

        var url = $"https://bot-api.zaloplatforms.com/bot{botToken}/sendSticker";
        
        var payload = new
        {
            chat_id = userId,
            sticker = stickerId
        };
        
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content, ct);
            
            var responseString = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Zalo API HTTP error {StatusCode}: {ErrorResponse}", response.StatusCode, responseString);
                return false;
            }
            
            try 
            {
                using var doc = JsonDocument.Parse(responseString);
                if (doc.RootElement.TryGetProperty("ok", out var okElement) && !okElement.GetBoolean())
                {
                    _logger.LogWarning("Zalo API rejected sticker: {ErrorResponse}", responseString);
                    return false;
                }
            } 
            catch { /* Not valid JSON, ignore */ }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Zalo sticker to UserId {UserId}", userId);
            return false;
        }
    }
}
