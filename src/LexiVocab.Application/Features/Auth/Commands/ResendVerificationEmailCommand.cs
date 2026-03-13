using System.Security.Cryptography;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace LexiVocab.Application.Features.Auth.Commands;

public record ResendVerificationEmailCommand(string Email) : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.UserUpdated;
    public string? EntityType => "User";
}

public class ResendVerificationEmailHandler : IRequestHandler<ResendVerificationEmailCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IDistributedCache _cache;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly string _appUrl;

    public ResendVerificationEmailHandler(
        IUnitOfWork uow,
        IDistributedCache cache,
        IEmailQueueService emailQueue,
        IEmailTemplateService templateService,
        IConfiguration configuration)
    {
        _uow = uow;
        _cache = cache;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _appUrl = configuration["App:Url"] ?? "https://lexivocab.store";
    }

    public async Task<Result> Handle(ResendVerificationEmailCommand request, CancellationToken ct)
    {
        var email = request.Email.ToLowerInvariant().Trim();
        var user = await _uow.Users.GetByEmailAsync(email, ct);

        // Security: Don't reveal if user exists or not if they are already confirmed
        if (user == null || user.EmailConfirmed)
            return Result.Success(); 

        var cooldownKey = $"email-verify-cooldown:{email}";
        var cooldown = await _cache.GetStringAsync(cooldownKey, ct);
        if (!string.IsNullOrEmpty(cooldown))
            return Result.Failure("Please wait 2 minutes before requesting another verification code.", 429);

        var cacheKey = $"email-verify:{email}";
        var verifyCode = await _cache.GetStringAsync(cacheKey, ct);

        if (string.IsNullOrEmpty(verifyCode))
        {
            verifyCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            await _cache.SetStringAsync(cacheKey, verifyCode, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) }, ct);
        }

        try
        {
            var html = await _templateService.RenderTemplateAsync("Welcome", new Dictionary<string, string>
            {
                { "FullName", user.FullName },
                { "Code", verifyCode },
                { "AppUrl", _appUrl }
            });
            _emailQueue.EnqueueEmail(user.Email, "LexiVocab: Your verification code 🚀", html);
            
            // Set cooldown
            await _cache.SetStringAsync(cooldownKey, "sent", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) }, ct);
        }
        catch 
        {
            return Result.Failure("Failed to send verification email. Please try again later.", 500);
        }

        return Result.Success();
    }
}
