using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Analytics;
using LexiVocab.Application.Features.Analytics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Comprehensive dashboard overview: vocabulary stats, review stats, streak, total study days.
    /// Suitable for caching with Redis (data changes at most once per minute during active use).
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDashboardQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// GitHub-style contribution heatmap — review counts per day for an entire year.
    /// </summary>
    [HttpGet("heatmap")]
    [ProducesResponseType(typeof(HeatmapDataDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeatmap([FromQuery] int? year, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetHeatmapQuery(year), ct);
        return ToActionResult(result);
    }

    /// <summary>Get current learning streak information.</summary>
    [HttpGet("streak")]
    [ProducesResponseType(typeof(StreakDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStreak(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStreakQuery(), ct);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
