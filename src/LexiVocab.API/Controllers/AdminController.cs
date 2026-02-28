using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Application.Features.Admin.Commands;
using LexiVocab.Application.Features.Admin.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Get paginated list of all users, with optional email/name search.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(PagedResult<UserOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetUsersQuery(page, pageSize, search), ct);
        return ToActionResult(result);
    }

    /// <summary>Get deep details of a single user: vocab stats, review logs, and subscriptions.</summary>
    [HttpGet("users/{id}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserDetailQuery(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Change a user's role: User, Premium, or Admin.</summary>
    [HttpPut("users/{id}/role")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateUserRoleCommand(id, request.Role), ct);
        return ToActionResult(result);
    }

    /// <summary>Activate or deactivate a user account (soft ban).</summary>
    [HttpPut("users/{id}/status")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateUserStatusCommand(id, request.IsActive), ct);
        return ToActionResult(result);
    }

    /// <summary>Manually add or gift a subscription to a user. Invalidates existing active ones.</summary>
    [HttpPost("users/{id}/subscriptions")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddSubscription(Guid id, [FromBody] AddSubscriptionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddManualSubscriptionCommand(id, request.Plan, request.DurationDays), ct);
        return ToActionResult(result);
    }

    /// <summary>Force cancel a user's currently active subscription and revert Premium privileges.</summary>
    [HttpDelete("users/{id}/subscriptions")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelSubscription(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelCurrentSubscriptionCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Get high-level system metrics to render the Admin Dashboard Overview.</summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(SystemStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemMetrics(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSystemStatsQuery(), ct);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
