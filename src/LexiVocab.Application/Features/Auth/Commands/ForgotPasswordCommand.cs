using System;
using System.Collections.Generic;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Features.Auth.Commands;

public record ForgotPasswordCommand(string Email) : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.UserUpdated;
    public string? EntityType => "User";
}

public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IDistributedCache _cache;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly ILogger<ForgotPasswordHandler> _logger;
    private readonly string _appUrl;

    public ForgotPasswordHandler(
        IUnitOfWork uow, 
        IDistributedCache cache, 
        IEmailQueueService emailQueue, 
        IEmailTemplateService templateService,
        IConfiguration configuration,
        ILogger<ForgotPasswordHandler> logger)
    {
        _uow = uow;
        _cache = cache;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _appUrl = configuration["App:Url"] ?? "https://lexivocab.store";
        _logger = logger;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByEmailAsync(request.Email.ToLowerInvariant().Trim(), ct);
        
        // Security: Always return success to prevent email enumeration attacks
        if (user == null || user.AuthProvider != null)
            return Result.Success();

        // Generate 6-digit code
        var code = new Random().Next(100000, 999999).ToString();
        var cacheKey = $"reset-pass:{user.Email}";

        await _cache.SetStringAsync(cacheKey, code, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        }, ct);

        try
        {
            var html = await _templateService.RenderTemplateAsync("ForgotPassword", new Dictionary<string, string>
            {
                { "FullName", user.FullName },
                { "Code", code },
                { "ExpiryHours", "1" },
                { "AppUrl", _appUrl }
            });

            _emailQueue.EnqueueEmail(user.Email, "Reset Your LexiVocab Password 🔑", html);
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "Failed to enqueue forgot password email for {Email}. Error: {Message}", user.Email, ex.Message);
        }

        return Result.Success();
    }
}
