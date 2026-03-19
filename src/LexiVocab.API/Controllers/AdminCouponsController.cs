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
public class AdminCouponsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminCouponsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a new discount coupon.</summary>
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
            request.IsActive,
            request.Description);

        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Update an existing coupon.</summary>
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
            request.IsActive,
            request.Description);

        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Delete a coupon.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCoupon(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCouponCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Get a coupon by ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdminCouponDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoupon(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAdminCouponByIdQuery(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Get paginated list of coupons.</summary>
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

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
