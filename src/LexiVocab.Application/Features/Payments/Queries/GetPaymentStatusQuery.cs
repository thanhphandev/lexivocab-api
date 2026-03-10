using LexiVocab.Application.Common;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Queries;

public record GetPaymentStatusQuery(string Reference) : IRequest<Result<string>>;

public class GetPaymentStatusHandler : IRequestHandler<GetPaymentStatusQuery, Result<string>>
{
    private readonly IUnitOfWork _uow;

    public GetPaymentStatusHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<string>> Handle(GetPaymentStatusQuery request, CancellationToken ct)
    {
        var tx = await _uow.PaymentTransactions
            .GetByExternalOrderIdAsync(request.Reference, ct);

        if (tx == null) return Result<string>.NotFound("Transaction not found.");

        return Result<string>.Success(tx.Status.ToString());
    }
}
