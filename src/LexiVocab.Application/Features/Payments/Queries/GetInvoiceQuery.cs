using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Enums;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Queries;

public record InvoiceFileDto(byte[] Bytes, string ContentType, string FileName);

public record GetInvoiceQuery(Guid TransactionId) : IRequest<Result<InvoiceFileDto>>;

public class GetInvoiceHandler : IRequestHandler<GetInvoiceQuery, Result<InvoiceFileDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetInvoiceHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<InvoiceFileDto>> Handle(GetInvoiceQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var tx = await _uow.PaymentTransactions.GetByIdAsync(request.TransactionId, ct);
        if (tx == null || tx.UserId != userId)
            return Result<InvoiceFileDto>.NotFound("Invoice not found.", ErrorCode.RESOURCE_NOT_FOUND);

        var csv = BuildCsv(tx, userId);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var fileName = $"invoice-{tx.ExternalOrderId}.csv";

        return Result<InvoiceFileDto>.Success(new InvoiceFileDto(bytes, "text/csv", fileName));
    }

    private static string BuildCsv(Domain.Entities.PaymentTransaction tx, Guid userId)
    {
        var lines = new[]
        {
            "Field,Value",
            $"Invoice ID,{tx.Id}",
            $"Transaction ID,{tx.ExternalOrderId}",
            $"User ID,{userId}",
            $"Provider,{tx.Provider}",
            $"Amount,{tx.Amount} {tx.Currency}",
            $"Status,{tx.Status}",
            $"Created At,{tx.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC",
            $"Paid At,{(tx.PaidAt.HasValue ? tx.PaidAt.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "N/A")}",
        };
        return string.Join("\n", lines);
    }
}
