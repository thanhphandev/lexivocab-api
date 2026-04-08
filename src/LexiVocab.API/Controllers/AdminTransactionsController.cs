using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Application.Features.Admin.Transactions.Queries;
using LexiVocab.Application.Features.Admin.Transactions.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Asp.Versioning;

namespace LexiVocab.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/transactions")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminTransactionsController : BaseApiController
{
    private readonly IMediator _mediator;

    public AdminTransactionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get all transactions.
    /// </summary>
    /// <param name="fromDate">Filter from date.</param>
    /// <param name="toDate">Filter to date.</param>
    /// <param name="status">Filter by status.</param>
    /// <param name="provider">Filter by provider.</param>
    /// <param name="search">Search by user or reference.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns paginated transactions.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null,
        [FromQuery] string? provider = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetAdminTransactionsQuery(fromDate, toDate, status, provider, search, page, pageSize);
        var result = await _mediator.Send(query, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Refund transaction.
    /// </summary>
    /// <param name="id">Transaction ID.</param>
    /// <param name="request">Refund reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Refund processed.</response>
    [HttpPost("{id}/refund")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefundTransaction(Guid id, [FromBody] RefundTransactionRequest request, CancellationToken ct)
    {
        var command = new ProcessRefundCommand(id, request.Reason);
        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }
}
