using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Application.Features.Admin.Transactions.Queries;
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
public class AdminTransactionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminTransactionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Get paginated list of transactions for admin.</summary>
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
        
        if (result.IsSuccess)
            return Ok(new { success = true, data = result.Data });
        return BadRequest(new { success = false, error = result.Error });
    }

    /// <summary>Issue a refund for a transaction.</summary>
    [HttpPost("{id}/refund")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefundTransaction(Guid id, [FromBody] RefundTransactionRequest request, CancellationToken ct)
    {
        var command = new LexiVocab.Application.Features.Admin.Transactions.Commands.ProcessRefundCommand(id, request.Reason);
        var result = await _mediator.Send(command, ct);
        
        if (result.IsSuccess)
            return Ok(new { success = true, message = "Refund processed successfully." });
        return BadRequest(new { success = false, error = result.Error });
    }
}
