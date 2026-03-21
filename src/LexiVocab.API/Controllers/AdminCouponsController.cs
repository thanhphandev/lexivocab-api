using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Application.Features.Admin.Coupons.Commands;
using LexiVocab.Application.Features.Admin.Coupons.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Asp.Versioning;

namespace LexiVocab.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/coupons")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminCouponsController : BaseApiController
{
    private readonly IMediator _mediator;

    public AdminCouponsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Create new coupon.
    /// </summary>
    /// <param name="request">Coupon configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Returns the created coupon.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AdminCouponDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponRequest request, CancellationToken ct)
    {
        var command = new CreateCouponCommand(
            request.Code,
            request.DiscountType,
            request.DiscountValue,
            request.MaxUses,
            request.ValidFrom,
            request.ValidUntil,
            request.Currency,
            request.IsActive,
            request.Description);

        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Update existing coupon.
    /// </summary>
    /// <param name="id">Coupon ID.</param>
    /// <param name="request">New coupon data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns updated coupon.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AdminCouponDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCoupon(Guid id, [FromBody] UpdateCouponRequest request, CancellationToken ct)
    {
        var command = new UpdateCouponCommand(
            id,
            request.Code,
            request.DiscountType,
            request.DiscountValue,
            request.MaxUses,
            request.ValidFrom,
            request.ValidUntil,
            request.Currency,
            request.IsActive,
            request.Description);

        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Delete coupon.
    /// </summary>
    /// <param name="id">Coupon ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Coupon deleted.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCoupon(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCouponCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get coupon by ID.
    /// </summary>
    /// <param name="id">Coupon ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns coupon details.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdminCouponDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoupon(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAdminCouponByIdQuery(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get all coupons.
    /// </summary>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="search">Search by code or description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns paginated coupons.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminCouponDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoupons(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminCouponsQuery(page, pageSize, search), ct);
        return ToActionResult(result);
    }

}
