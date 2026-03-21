using System.Text;
using System.Text.Json;
using LexiVocab.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services.Notifications;

public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(HttpClient httpClient, ILogger<TelegramNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendMessageAsync(string botToken, string chatId, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            return false;

        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
        
        var payload = new
        {
            chat_id = chatId,
            text = message,
            parse_mode = "HTML"
        };
        
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorString = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Telegram API returned {StatusCode}: {ErrorResponse}", response.StatusCode, errorString);
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message to ChatId {ChatId}", chatId);
            return false;
        }
    }

    public async Task<bool> SendStickerAsync(string botToken, string chatId, string stickerUrlOrId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(stickerUrlOrId))
            return false;

        var url = $"https://api.telegram.org/bot{botToken}/sendSticker";
        
        var payload = new
        {
            chat_id = chatId,
            sticker = stickerUrlOrId
        };
        
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorString = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Telegram API returned {StatusCode} for SendSticker: {ErrorResponse}", response.StatusCode, errorString);
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram sticker to ChatId {ChatId}", chatId);
            return false;
        }
    }
}
