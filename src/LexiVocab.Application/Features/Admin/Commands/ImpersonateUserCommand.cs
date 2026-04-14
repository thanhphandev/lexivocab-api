using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Commands;

public record ImpersonateUserCommand(Guid TargetUserId) : IRequest<Result<AuthResponse>>;

public class ImpersonateUserHandler : IRequestHandler<ImpersonateUserCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditLogService _auditLogService;

    public ImpersonateUserHandler(
        IUnitOfWork uow, 
        ICurrentUserService currentUser,
        IJwtTokenService jwtTokenService,
        IAuditLogService auditLogService)
    {
        _uow = uow;
        _currentUser = currentUser;
        _jwtTokenService = jwtTokenService;
        _auditLogService = auditLogService;
    }

    public async Task<Result<AuthResponse>> Handle(ImpersonateUserCommand request, CancellationToken ct)
    {
        var adminId = _currentUser.UserId;
        if (adminId == null) 
            return Result<AuthResponse>.Unauthorized();

        var targetUser = await _uow.Users.GetByIdAsync(request.TargetUserId, ct);
        if (targetUser == null)
            return Result<AuthResponse>.NotFound("Target user not found.", ErrorCode.RESOURCE_NOT_FOUND);

        if (targetUser.Role == UserRole.Admin)
            return Result<AuthResponse>.Forbidden("You cannot impersonate another administrator.", ErrorCode.AUTHZ_ADMIN_ONLY);

        // Generate Impersonation Token
        var tokenResult = _jwtTokenService.GenerateImpersonationToken(
            targetUser.Id, 
            targetUser.Email, 
            targetUser.Role.ToString(), 
            adminId.Value);

        // Record Audit Log
        await _auditLogService.LogAsync(
            action: AuditAction.ImpersonateUser,
            userId: adminId.Value,
            entityType: "User",
            entityId: targetUser.Id.ToString(),
            additionalInfo: $"Admin impersonated user {targetUser.Email}",
            ct: ct);

        // Return standard Auth format but without a refresh token
        var response = new AuthResponse(
            targetUser.Id,
            targetUser.Email,
            targetUser.FullName,
            targetUser.Role.ToString(),
            tokenResult.Token,
            null, 
            tokenResult.ExpiresAt,
            targetUser.AvatarUrl,
            targetUser.EmailConfirmed,
            targetUser.IsActive
        );

        return Result<AuthResponse>.Success(response);
    }
}
