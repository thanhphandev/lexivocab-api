using LexiVocab.Application.DTOs.Analytics;
using LexiVocab.Application.Features.Analytics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Asp.Versioning;

namespace LexiVocab.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[Produces("application/json")]
public class AnalyticsController : BaseApiController
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Get dashboard overview.
    /// </summary>
    /// <remarks>
    /// Returns a comprehensive set of statistics including vocabulary counts, review history summary, 
    /// current streak, and total study days.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns dashboard data.</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDashboardQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get learning heatmap.
    /// </summary>
    /// <remarks>
    /// Returns GitHub-style contribution data (review counts per day) for the specified year.
    /// </remarks>
    /// <param name="year">Year to fetch data for (defaults to current year).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns heatmap data.</response>
    [HttpGet("heatmap")]
    [ProducesResponseType(typeof(HeatmapDataDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeatmap([FromQuery] int? year, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetHeatmapQuery(year), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get current learning streak.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns streak info.</response>
    [HttpGet("streak")]
    [ProducesResponseType(typeof(StreakDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStreak(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStreakQuery(), ct);
        return ToActionResult(result);
    }

}
