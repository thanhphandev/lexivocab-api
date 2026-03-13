using System;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Auth.Commands;

public record VerifyEmailCommand(string Email, string Code) : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.UserUpdated;
    public string? EntityType => "User";
}

public class VerifyEmailHandler : IRequestHandler<VerifyEmailCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IDistributedCache _cache;

    public VerifyEmailHandler(IUnitOfWork uow, IDistributedCache cache)
    {
        _uow = uow;
        _cache = cache;
    }

    public async Task<Result> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var email = request.Email.ToLowerInvariant().Trim();
        var cacheKey = $"email-verify:{email}";

        var savedCode = await _cache.GetStringAsync(cacheKey, ct);
        var attemptsKey = $"email-verify-attempts:{email}";
        
        if (string.IsNullOrEmpty(savedCode))
            return Result.Failure("Invalid or expired verification code.", 400);

        if (savedCode != request.Code)
        {
            var attemptsStr = await _cache.GetStringAsync(attemptsKey, ct);
            int attempts = string.IsNullOrEmpty(attemptsStr) ? 0 : int.Parse(attemptsStr);
            attempts++;

            if (attempts >= 5)
            {
                await _cache.RemoveAsync(cacheKey, ct);
                await _cache.RemoveAsync(attemptsKey, ct);
                return Result.Failure("Too many failed attempts. Please request a new verification code.", 429);
            }

            await _cache.SetStringAsync(attemptsKey, attempts.ToString(), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) }, ct);
            return Result.Failure("Invalid verification code.", 400);
        }

        var user = await _uow.Users.GetByEmailAsync(email, ct);
        if (user == null)
            return Result.NotFound("User no longer exists.");

        if (user.EmailConfirmed)
            return Result.Success();

        user.EmailConfirmed = true;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        await _cache.RemoveAsync(cacheKey, ct);
        await _cache.RemoveAsync(attemptsKey, ct);

        return Result.Success();
    }
}
