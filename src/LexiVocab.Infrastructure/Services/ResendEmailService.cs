using LexiVocab.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace LexiVocab.Infrastructure.Services;

public class ResendEmailService : IEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<ResendEmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var apiKey = _config["Resend:ApiKey"] ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");
        var senderName = _config["Resend:SenderName"] ?? Environment.GetEnvironmentVariable("RESEND_SENDER_NAME") ?? "LexiVocab";
        var senderEmail = _config["Resend:SenderEmail"] ?? Environment.GetEnvironmentVariable("RESEND_SENDER_EMAIL");

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(senderEmail))
        {
            _logger.LogError("Resend Configuration is missing. Cannot send email to {To}", to);
            throw new InvalidOperationException("Resend Configuration is invalid.");
        }

        var client = _httpClientFactory.CreateClient("ResendClient");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            @from = $"{senderName} <{senderEmail}>",
            to = new[] { to },
            subject = subject,
            html = htmlBody
        };

        var json = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Email successfully sent via Resend to {To}", to);
        }
        else
        {
            var errorContext = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Error sending email via Resend to {To}. Status: {StatusCode}. Response: {Response}", to, response.StatusCode, errorContext);
            response.EnsureSuccessStatusCode(); 
        }
    }
}
