using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Application.Features.Payments.Commands;
using LexiVocab.Application.Features.Payments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LexiVocab.Domain.Enums;

using Asp.Versioning;

namespace LexiVocab.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IMediator mediator, ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Cancel the current user's active subscription immediately.</summary>
    [HttpPost("cancel-subscription")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelMySubscription(CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelMySubscriptionCommand(), ct);
        if (result.IsSuccess)
            return Ok(new { success = true, message = "Subscription cancelled successfully." });
        return ToActionResult(result);
    }

    /// <summary>Download a CSV invoice for a specific payment transaction.</summary>
    [HttpGet("invoice/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInvoiceQuery(id), ct);
        if (result.IsSuccess)
            return File(result.Data!.Bytes, result.Data.ContentType, result.Data.FileName);
        return ToActionResult(result);
    }

    /// <summary>Get billing overview: subscription status, plan info, transaction count.</summary>
    [HttpGet("billing")]
    [ProducesResponseType(typeof(BillingOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBillingOverview(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBillingOverviewQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>Get paginated payment transaction history.</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedResult<PaymentHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var result = await _mediator.Send(new GetPaymentHistoryQuery(page, pageSize), ct);
        return ToActionResult(result);
    }

    /// <summary>Get available subscription plans dynamically.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriptionPlans(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSubscriptionPlansQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>Create a PayPal payment order and return the approval URL.</summary>
    [HttpPost("create-order")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var result = await _mediator.Send(new CreatePaymentOrderCommand(request.PricingId, request.Provider));
        return result.IsSuccess ? Ok(new { approvalUrl = result.Data }) : ToActionResult(result); // Changed to use controller's ToActionResult
    }

    [HttpGet("status/{reference}")]
    public async Task<IActionResult> GetStatus(string reference)
    {
        var result = await _mediator.Send(new GetPaymentStatusQuery(reference));
        return ToActionResult(result);
    }

    /// <summary>Cancel a pending payment transaction (user-initiated).</summary>
    [HttpPost("cancel/{reference}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelPayment(string reference, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelPaymentCommand(reference), ct);
        return ToActionResult(result);
    }

    /// <summary>Capture a PayPal order after user approval.</summary>
    [HttpPost("capture-order")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CaptureOrder([FromBody] CaptureOrderRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CapturePaymentOrderCommand(request.OrderId), ct);
        return ToActionResult(result);
    }

    /// <summary>PayPal webhook endpoint for async payment notifications.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<IActionResult> PayPalWebhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var command = new ProcessPaymentWebhookCommand(PaymentProvider.PayPal, body, headers);
        
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok() : ToActionResult(result);
    }

    // ─── Sepay Webhook ──────────────────────────────────────────
    [HttpPost("webhook/sepay")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<IActionResult> SepayWebhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var command = new ProcessPaymentWebhookCommand(PaymentProvider.Sepay, body, headers);
        
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok() : ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess) return Ok(new { success = true });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
