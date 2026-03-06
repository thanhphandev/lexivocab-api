namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Core email sending service (SMTP logic).
/// </summary>
public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
