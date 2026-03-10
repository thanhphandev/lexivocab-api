using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Auth.Queries;

public record GetCurrentUserQuery : IRequest<Result<UserProfileDto>>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserProfileDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentUserQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<UserProfileDto>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<UserProfileDto>.Unauthorized();

        var user = await _uow.Users.GetByIdAsync(_currentUser.UserId.Value, ct);
        if (user is null)
            return Result<UserProfileDto>.NotFound("User not found.");

        return Result<UserProfileDto>.Success(new UserProfileDto(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            user.IsActive, user.CreatedAt, user.LastLogin));
    }
}
