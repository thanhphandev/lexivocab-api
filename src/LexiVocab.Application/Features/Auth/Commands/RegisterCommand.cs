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
    private readonly string _appUrl;

    public RegisterCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IDistributedCache cache, IEmailQueueService emailQueue, IEmailTemplateService templateService, IConfiguration configuration)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _cache = cache;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _appUrl = configuration["App:Url"] ?? "https://lexivocab.store";
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (await _uow.Users.EmailExistsAsync(request.Email, ct))
            return Result<AuthResponse>.Conflict($"Email '{request.Email}' is already registered.");

        var user = new Domain.Entities.User
        {
            Email = request.Email.ToLowerInvariant().Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            LastLogin = DateTime.UtcNow
        };

        await _uow.Users.AddAsync(user, ct);

        // Auto-create default settings for new user
        user.UserSetting = new Domain.Entities.UserSetting { UserId = user.Id };

        await _uow.SaveChangesAsync(ct);

        // Enqueue welcome email using template (returns immediately, processed in background)
        try
        {
            var html = await _templateService.RenderTemplateAsync("Welcome", new Dictionary<string, string>
            {
                { "FullName", user.FullName },
                { "AppUrl", _appUrl }
            });
            _emailQueue.EnqueueEmail(user.Email, "Welcome to LexiVocab! 🚀", html);
        }
        catch { /* Non-critical: don't block registration if template fails */ }

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var refreshToken = _jwt.GenerateRefreshToken();

        user.RefreshTokenHash = _hasher.Hash(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _uow.SaveChangesAsync(ct);

        var metadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, request.DeviceInfo, request.IpAddress, DateTime.UtcNow));
        await _cache.SetStringAsync($"rf_token:{refreshToken}", metadata, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) }, ct);

        return Result<AuthResponse>.Created(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, refreshToken, DateTime.UtcNow.AddHours(1)));
    }
}
