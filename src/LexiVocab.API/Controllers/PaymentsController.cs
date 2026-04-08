using LexiVocab.Application.Common;
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
public class PaymentsController : BaseApiController
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IMediator mediator, ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Cancel active subscription.
    /// </summary>
    /// <remarks>
    /// Cancels the current user's active subscription immediately. 
    /// Access to Premium features will be revoked at the end of the current billing cycle.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Subscription cancelled.</response>
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

    /// <summary>
    /// Get invoice for a transaction.
    /// </summary>
    /// <param name="id">Transaction ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the invoice file (CSV/PDF).</response>
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

    /// <summary>
    /// Get billing overview.
    /// </summary>
    /// <remarks>
    /// Returns current subscription status, plan information, and transaction summary.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns billing overview.</response>
    [HttpGet("billing")]
    [ProducesResponseType(typeof(BillingOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBillingOverview(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBillingOverviewQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get payment history.
    /// </summary>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns paginated transaction history.</response>
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

    /// <summary>
    /// Get available subscription plans.
    /// </summary>
    /// <remarks>
    /// Public endpoint to fetch active pricing plans and their features.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns list of plans.</response>
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriptionPlans(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSubscriptionPlansQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Create a payment order.
    /// </summary>
    /// <remarks>
    /// Initiates a payment process with the selected provider (PayPal, Sepay).
    /// Returns an approval URL or payment instructions.
    /// </remarks>
    /// <param name="request">Order details (Pricing ID, Provider, Coupon).</param>
    /// <response code="200">Returns the approval URL or payment data.</response>
    [HttpPost("create-order")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var result = await _mediator.Send(new CreatePaymentOrderCommand(request.PricingId, request.Provider, request.CouponCode));
        return result.IsSuccess ? Ok(new { approvalUrl = result.Data }) : ToActionResult(result); // Changed to use controller's ToActionResult
    }

    /// <summary>
    /// Get payment status by reference.
    /// </summary>
    /// <param name="reference">Order/Transaction reference.</param>
    /// <response code="200">Returns current status.</response>
    [HttpGet("status/{reference}")]
    public async Task<IActionResult> GetStatus(string reference)
    {
        var result = await _mediator.Send(new GetPaymentStatusQuery(reference));
        return ToActionResult(result);
    }

    /// <summary>
    /// Cancel a pending payment.
    /// </summary>
    /// <param name="reference">Order reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Payment cancelled.</response>
    [HttpPost("cancel/{reference}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelPayment(string reference, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelPaymentCommand(reference), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Capture approved PayPal order.
    /// </summary>
    /// <param name="request">PayPal Order ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Order captured and subscription activated.</response>
    [HttpPost("capture-order")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CaptureOrder([FromBody] CaptureOrderRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CapturePaymentOrderCommand(request.OrderId), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// PayPal Webhook.
    /// </summary>
    /// <remarks>
    /// Async notification endpoint for PayPal events.
    /// </remarks>
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

    /// <summary>
    /// Sepay Webhook (Vietnamese Banks).
    /// </summary>
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

}
