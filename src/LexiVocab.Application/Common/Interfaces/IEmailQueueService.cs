namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Background queue service for non-blocking email delivery.
/// </summary>
public interface IEmailQueueService
{
    /// <summary>
    /// Enqueues an email to be sent in the background. Returns immediately.
    /// </summary>
    /// <returns>The ID of the background job.</returns>
    string EnqueueEmail(string to, string subject, string htmlBody);
}
