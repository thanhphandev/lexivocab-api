using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using MediatR;

namespace LexiVocab.Application.Features.Auth.Queries;

public record GetUserPermissionsQuery() : IRequest<Result<UserPermissionsDto>>;

public class GetUserPermissionsHandler : IRequestHandler<GetUserPermissionsQuery, Result<UserPermissionsDto>>
{
    private readonly IFeatureGatingService _featureGating;
    private readonly ICurrentUserService _currentUser;

    public GetUserPermissionsHandler(IFeatureGatingService featureGating, ICurrentUserService currentUser)
    {
        _featureGating = featureGating;
        _currentUser = currentUser;
    }

    public async Task<Result<UserPermissionsDto>> Handle(GetUserPermissionsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var permissions = await _featureGating.GetPermissionsAsync(userId, ct);
        return Result<UserPermissionsDto>.Success(permissions);
    }
}
