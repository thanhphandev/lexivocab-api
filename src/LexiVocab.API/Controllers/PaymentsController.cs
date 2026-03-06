using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Application.Features.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IMediator mediator, IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _paymentService = paymentService;
        _logger = logger;
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
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePaymentOrderCommand(request.PlanId), ct);
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

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        var isValid = await _paymentService.VerifyWebhookSignatureAsync(body, headers);
        if (!isValid)
        {
            _logger.LogWarning("Invalid webhook signature detected.");
            return BadRequest();
        }

        _logger.LogInformation("Received verified PayPal webhook. Processing event...");
        await _paymentService.ProcessWebhookEventAsync(body, ct);
        return Ok();
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
