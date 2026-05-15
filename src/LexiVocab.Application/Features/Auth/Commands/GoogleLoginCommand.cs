using System.Text.Json;
using System.Text.Json.Serialization;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Helpers;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Features.Auth.Commands;

public record GoogleLoginCommand(
    [property: JsonIgnore] string IdToken,
    [property: JsonIgnore] string DeviceInfo,
    [property: JsonIgnore] string IpAddress)
    : IRequest<Result<AuthResponse>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.GoogleLogin;
    public string? EntityType => "User";
}

public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IAuthTokenService _authTokenService;
    private readonly IGoogleAuthService _googleAuth;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleLoginCommandHandler> _logger;
    private readonly string _appUrl;

    public GoogleLoginCommandHandler(
        IUnitOfWork uow, 
        IAuthTokenService authTokenService,
        IGoogleAuthService googleAuth, 
        IEmailQueueService emailQueue, 
        IEmailTemplateService templateService, 
        IConfiguration configuration, 
        ILogger<GoogleLoginCommandHandler> logger)
    {
        _uow = uow;
        _authTokenService = authTokenService;
        _googleAuth = googleAuth;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _configuration = configuration;
        _logger = logger;
        _appUrl = configuration["App:Url"] ?? "https://lexivocab.store";
    }

    public async Task<Result<AuthResponse>> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        var googleUser = await _googleAuth.ValidateIdTokenAsync(request.IdToken, ct);
        if (googleUser is null)
            return Result<AuthResponse>.Unauthorized("Invalid Google ID token.", ErrorCode.AUTH_GOOGLE_TOKEN_INVALID);

        var user = await _uow.Users.GetByAuthProviderAsync("Google", googleUser.Subject, ct);

        if (user is null)
        {
            user = await _uow.Users.GetByEmailAsync(googleUser.Email.ToLowerInvariant(), ct);
            if (user is not null)
            {
                user.AuthProvider = "Google";
                user.AuthProviderId = googleUser.Subject;

                if (string.IsNullOrEmpty(user.AvatarUrl) && !string.IsNullOrEmpty(googleUser.PictureUrl))
                {
                    user.AvatarUrl = googleUser.PictureUrl;
                }
            }
            else
            {
                user = new User
                {
                    Email = googleUser.Email.ToLowerInvariant(),
                    FullName = googleUser.FullName,
                    AvatarUrl = googleUser.PictureUrl,
                    AuthProvider = "Google",
                    AuthProviderId = googleUser.Subject,
                    EmailConfirmed = true
                };
                await _uow.Users.AddAsync(user, ct);
                user.UserSetting = new UserSetting { UserId = user.Id };

                try
                {
                    var html = await _templateService.RenderTemplateAsync("WelcomeVerified", new Dictionary<string, string>
                    {
                        { "FullName", user.FullName },
                        { "AppUrl", _appUrl }
                    });
                    _emailQueue.EnqueueEmail(user.Email, "Welcome to LexiVocab! 🚀", html);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to send welcome email for new Google user {Email}", user.Email); }
            }
        }
        else
        {
            if (string.IsNullOrEmpty(user.AvatarUrl) && !string.IsNullOrEmpty(googleUser.PictureUrl))
            {
                user.AvatarUrl = googleUser.PictureUrl;
            }
        }

        if (!user.IsActive)
            return Result<AuthResponse>.Forbidden("Account is deactivated.", ErrorCode.AUTH_ACCOUNT_DISABLED);

        var authResponse = await _authTokenService.IssueTokenPairAsync(user, request.DeviceInfo, request.IpAddress, ct);

        return Result<AuthResponse>.Success(authResponse);
    }
}
