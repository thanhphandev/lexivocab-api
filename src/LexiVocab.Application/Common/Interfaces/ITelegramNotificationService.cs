namespace LexiVocab.Application.Common.Interfaces;

public interface ITelegramNotificationService
{
    Task<bool> SendMessageAsync(string botToken, string chatId, string message, CancellationToken ct = default);
    Task<bool> SendStickerAsync(string botToken, string chatId, string stickerUrlOrId, CancellationToken ct = default);
}
