using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Queries;

public record GetUserDetailQuery(Guid UserId) : IRequest<Result<UserDetailDto>>;

public class GetUserDetailHandler : IRequestHandler<GetUserDetailQuery, Result<UserDetailDto>>
{
    private readonly IUnitOfWork _uow;

    public GetUserDetailHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<UserDetailDto>> Handle(GetUserDetailQuery request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);

        if (user == null)
            return Result<UserDetailDto>.Failure("User not found", 404);

        var totalVocabularies = await _uow.Vocabularies.CountByUserIdAsync(request.UserId, ct);
        var totalReviews = await _uow.ReviewLogs.CountByUserIdAsync(request.UserId, ct);

        var subs = await _uow.Subscriptions.GetByUserIdAsync(request.UserId, ct);
        
        var subscriptionDtos = subs.Select(s => new AdminSubscriptionDto(
            s.Id,
            s.PlanDefinition.Name,
            s.Status.ToString(),
            s.StartDate,
            s.EndDate,
            s.Provider.ToString(),
            s.ExternalSubscriptionId)).ToList();

        var dto = new UserDetailDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            user.IsActive,
            user.LastLogin,
            user.CreatedAt,
            user.AuthProvider,
            totalVocabularies,
            totalReviews,
            subscriptionDtos);

        return Result<UserDetailDto>.Success(dto);
    }
}
