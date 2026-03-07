using LexiVocab.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace LexiVocab.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var server = _config["Smtp:Server"];
        var port = _config.GetValue<int>("Smtp:Port", 587);
        var username = _config["Smtp:Username"];
        var password = _config["Smtp:Password"];
        var senderName = _config["Smtp:SenderName"] ?? "LexiVocab";
        var senderEmail = _config["Smtp:SenderEmail"] ?? username;
        var useSsl = _config.GetValue<bool>("Smtp:UseSsl", false);

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogError("SMTP Configuration is missing. Cannot send email to {To}", to);
            // In a real prod environment, throwing here is okay as Hangfire will catch, log, and retry if config changes.
            throw new InvalidOperationException("SMTP Configuration is invalid.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail!));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            // Connect
            var secureSocketOptions = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(server!, port, secureSocketOptions, ct);

            // Authenticate (Remove XOAUTH2 as a mechanism if we use standard username/pwd)
            client.AuthenticationMechanisms.Remove("XOAUTH2");
            await client.AuthenticateAsync(username!, password!, ct);

            // Send
            await client.SendAsync(message, ct);
            _logger.LogInformation("Email successfully sent to {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {To}", to);
            throw; // Hangfire will catch this and auto-retry
        }
        finally
        {
            await client.DisconnectAsync(true, ct);
        }
    }
}
