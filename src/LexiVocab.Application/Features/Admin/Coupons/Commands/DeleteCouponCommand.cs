using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Coupons.Commands;

public record DeleteCouponCommand(Guid Id) : IRequest<Result<bool>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(Domain.Entities.Coupon);
    public string EntityId => Id.ToString();
}

public class DeleteCouponHandler : IRequestHandler<DeleteCouponCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;

    public DeleteCouponHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(DeleteCouponCommand request, CancellationToken ct)
    {
        var coupon = await _uow.Coupons.GetByIdAsync(request.Id, ct);
        if (coupon == null)
            return Result<bool>.NotFound($"Coupon with ID '{request.Id}' not found.");

        _uow.Coupons.Remove(coupon);
        await _uow.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
