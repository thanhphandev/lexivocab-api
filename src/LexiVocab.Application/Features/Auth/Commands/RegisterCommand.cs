using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Features.Auth.Commands;

public record RegisterCommand(
    string Email,
    [property: JsonIgnore] string Password,
    string FullName,
    [property: JsonIgnore] string DeviceInfo,
    [property: JsonIgnore] string IpAddress)
    : IRequest<Result<AuthResponse>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.Register;
    public string? EntityType => "User";
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly IDistributedCache _cache;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RegisterCommandHandler> _logger;
    private readonly string _appUrl;

    public RegisterCommandHandler(
        IUnitOfWork uow, 
        IJwtTokenService jwt, 
        IPasswordHasher hasher, 
        IDistributedCache cache, 
        IEmailQueueService emailQueue, 
        IEmailTemplateService templateService, 
        IConfiguration configuration,
        ILogger<RegisterCommandHandler> logger)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _cache = cache;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _configuration = configuration;
        _logger = logger;
        _appUrl = configuration["App:Url"] ?? "https://lexivocab.store";
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (await _uow.Users.EmailExistsAsync(request.Email, ct))
            return Result<AuthResponse>.Conflict($"Email '{request.Email}' is already registered.");

        var requireVerification = _configuration["Auth:RequireEmailVerification"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        var user = new Domain.Entities.User
        {
            Email = request.Email.ToLowerInvariant().Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            LastLogin = DateTime.UtcNow,
            EmailConfirmed = !requireVerification
        };

        await _uow.Users.AddAsync(user, ct);

        // Auto-create default settings for new user
        user.UserSetting = new UserSetting { UserId = user.Id };

        await _uow.SaveChangesAsync(ct);

        if (requireVerification)
        {
            // Enqueue verification email (returns immediately, processed in background)
            var verifyCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            await _cache.SetStringAsync($"email-verify:{user.Email}", verifyCode, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) }, ct);

            try
            {
                var html = await _templateService.RenderTemplateAsync("Welcome", new Dictionary<string, string>
                {
                    { "FullName", user.FullName },
                    { "Code", verifyCode },
                    { "AppUrl", _appUrl }
                });
                _emailQueue.EnqueueEmail(user.Email, "Welcome to LexiVocab! Please verify your email 🚀", html);
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
            }

            return Result<AuthResponse>.Created(new AuthResponse(
                user.Id, user.Email, user.FullName, user.Role.ToString(),
                null, null, null));
        }
        else
        {
            try
            {
                var html = await _templateService.RenderTemplateAsync("WelcomeVerified", new Dictionary<string, string>
                {
                    { "FullName", user.FullName },
                    { "AppUrl", _appUrl }
                });
                _emailQueue.EnqueueEmail(user.Email, "Welcome to LexiVocab! 🚀", html);
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Failed to send welcome email to {Email}", user.Email);
            }
        }

        var accessTokenResult = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var accessToken = accessTokenResult.Token;
        var accessTokenExpiry = accessTokenResult.ExpiresAt;
        var refreshToken = _jwt.GenerateRefreshToken();

        var refreshTokenExpiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");
        user.RefreshTokenHash = _hasher.Hash(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);
        await _uow.SaveChangesAsync(ct);

        var metadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, request.DeviceInfo, request.IpAddress, DateTime.UtcNow));
        await _cache.SetStringAsync($"rf_token:{refreshToken}", metadata, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(refreshTokenExpiryDays) }, ct);

        return Result<AuthResponse>.Created(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, refreshToken, accessTokenExpiry));
    }
}
