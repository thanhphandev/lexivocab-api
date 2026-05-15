using System;
using System.Text.Json;
using LexiVocab.Application.Common.Helpers;
using System.Text.Json.Serialization;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace LexiVocab.Application.Features.Auth.Commands;

public record LoginCommand(
    string Email,
    [property: JsonIgnore] string Password,
    [property: JsonIgnore] string DeviceInfo,
    [property: JsonIgnore] string IpAddress)
    : IRequest<Result<AuthResponse>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.Login;
    public string? EntityType => "User";
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IAuthTokenService _authTokenService;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTimeProvider _dateTime;

    public LoginCommandHandler(IUnitOfWork uow, IAuthTokenService authTokenService, IPasswordHasher hasher, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _authTokenService = authTokenService;
        _hasher = hasher;
        _dateTime = dateTime;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByEmailAsync(request.Email.ToLowerInvariant().Trim(), ct);

        if (user == null || !user.IsActive)
            return Result<AuthResponse>.Failure("Invalid credentials or account deactivated.", 401, ErrorCode.AUTH_INVALID_CREDENTIALS);

        if (user.LockoutEnd.HasValue && user.LockoutEnd > _dateTime.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((user.LockoutEnd.Value - _dateTime.UtcNow).TotalMinutes);
            return Result<AuthResponse>.Failure($"Account is temporarily locked. Please try again in {remainingMinutes} minutes.", 423, ErrorCode.AUTH_ACCOUNT_LOCKED);
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
            {
                user.LockoutEnd = _dateTime.UtcNow.AddMinutes(15);
                user.AccessFailedCount = 0;
            }
            _uow.Users.Update(user);
            await _uow.SaveChangesAsync(ct);
            return Result<AuthResponse>.Failure("Invalid email or password.", 401, ErrorCode.AUTH_INVALID_CREDENTIALS);
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        var authResponse = await _authTokenService.IssueTokenPairAsync(user, request.DeviceInfo, request.IpAddress, ct);

        return Result<AuthResponse>.Success(authResponse);
    }
}
