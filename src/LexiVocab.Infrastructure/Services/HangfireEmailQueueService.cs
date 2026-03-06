using Hangfire;
using LexiVocab.Application.Common.Interfaces;

namespace LexiVocab.Infrastructure.Services;

public class HangfireEmailQueueService : IEmailQueueService
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireEmailQueueService(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public string EnqueueEmail(string to, string subject, string htmlBody)
    {
        // Enqueues the job. Hangfire resolves IEmailService from the DI container at execution time.
        return _backgroundJobClient.Enqueue<IEmailService>(
            emailService => emailService.SendEmailAsync(to, subject, htmlBody, CancellationToken.None));
    }
}
