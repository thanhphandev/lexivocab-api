using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Review;
using LexiVocab.Application.Features.Reviews.Commands;
using LexiVocab.Application.Features.Reviews.Queries;
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
public class ReviewsController : BaseApiController
{
    private readonly IMediator _mediator;

    public ReviewsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Get today's review session.
    /// </summary>
    /// <remarks>
    /// Fetches flashcards that are due for review (NextReviewDate ≤ NOW).
    /// Uses SRS (Spaced Repetition System) logic.
    /// </remarks>
    /// <param name="limit">Max cards to fetch (default: 50, max: 200).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns list of cards for review.</response>
    [HttpGet("session")]
    [ProducesResponseType(typeof(ReviewSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSession([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        var result = await _mediator.Send(new GetReviewSessionQuery(limit), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Submit review result.
    /// </summary>
    /// <remarks>
    /// Updates the word's SRS metadata (EaseFactor, Interval, Step) based on user performance.
    /// </remarks>
    /// <param name="request">Review details (Score 0-5, Time spent).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the updated SRS status.</response>
    /// <response code="404">Word not found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitReview([FromBody] SubmitReviewRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new SubmitReviewCommand(request.UserVocabularyId, request.QualityScore, request.TimeSpentMs), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get review history.
    /// </summary>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns paginated review logs.</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedResult<ReviewHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetReviewHistoryQuery(page, pageSize), ct);
        return ToActionResult(result);
    }

}
