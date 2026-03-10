using System.Text.Json;
using System.Text.Json.Serialization;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

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
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly IGoogleAuthService _googleAuth;
    private readonly IDistributedCache _cache;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly string _appUrl;

    public GoogleLoginCommandHandler(
        IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IGoogleAuthService googleAuth, IDistributedCache cache, IEmailQueueService emailQueue, IEmailTemplateService templateService, IConfiguration configuration)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _googleAuth = googleAuth;
        _cache = cache;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _appUrl = configuration["App:Url"] ?? "https://lexivocab.store";
    }

    public async Task<Result<AuthResponse>> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        var googleUser = await _googleAuth.ValidateIdTokenAsync(request.IdToken, ct);
        if (googleUser is null)
            return Result<AuthResponse>.Unauthorized("Invalid Google ID token.");

        var user = await _uow.Users.GetByAuthProviderAsync("Google", googleUser.Subject, ct);

        if (user is null)
        {
            user = await _uow.Users.GetByEmailAsync(googleUser.Email.ToLowerInvariant(), ct);
            if (user is not null)
            {
                user.AuthProvider = "Google";
                user.AuthProviderId = googleUser.Subject;
            }
            else
            {
                user = new Domain.Entities.User
                {
                    Email = googleUser.Email.ToLowerInvariant(),
                    FullName = googleUser.FullName,
                    AuthProvider = "Google",
                    AuthProviderId = googleUser.Subject,
                    LastLogin = DateTime.UtcNow
                };
                await _uow.Users.AddAsync(user, ct);
                user.UserSetting = new Domain.Entities.UserSetting { UserId = user.Id };

                try
                {
                    var html = await _templateService.RenderTemplateAsync("Welcome", new Dictionary<string, string>
                    {
                        { "FullName", user.FullName },
                        { "AppUrl", _appUrl }
                    });
                    _emailQueue.EnqueueEmail(user.Email, "Welcome to LexiVocab! 🚀", html);
                }
                catch { /* Non-critical */ }
            }
        }

        if (!user.IsActive)
            return Result<AuthResponse>.Forbidden("Account is deactivated.");

        user.LastLogin = DateTime.UtcNow;

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var refreshToken = _jwt.GenerateRefreshToken();

        user.RefreshTokenHash = _hasher.Hash(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        var metadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, request.DeviceInfo, request.IpAddress, DateTime.UtcNow));
        await _cache.SetStringAsync($"rf_token:{refreshToken}", metadata, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) }, ct);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, refreshToken, DateTime.UtcNow.AddHours(1)));
    }
}
