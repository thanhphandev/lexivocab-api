
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using LexiVocab.Application.Features.Coupons.Queries;

namespace LexiVocab.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/coupons")]
[Produces("application/json")]
public class CouponsController : BaseApiController
{
    private readonly IMediator _mediator;

    public CouponsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Validate a coupon code and get its discount details.</summary>
    [HttpGet("validate")]
    [ProducesResponseType(typeof(CouponValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EnableRateLimiting("UserReadLimit")]
    public async Task<IActionResult> ValidateCoupon([FromQuery] string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { success = false, error = "Coupon code is required." });

        var result = await _mediator.Send(new ValidateCouponQuery(code), ct);
        return ToActionResult(result);
    }
}
