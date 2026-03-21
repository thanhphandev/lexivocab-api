namespace LexiVocab.Application.Common.Interfaces;

public interface IZaloNotificationService
{
    Task<bool> SendMessageAsync(string botToken, string userId, string message, CancellationToken ct = default);
    Task<bool> SendStickerAsync(string botToken, string userId, string stickerId, CancellationToken ct = default);
}
