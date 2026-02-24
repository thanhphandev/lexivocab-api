using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Review;
using LexiVocab.Application.Features.Reviews.Commands;
using LexiVocab.Application.Features.Reviews.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReviewsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Get today's review session — flashcards where NextReviewDate ≤ NOW().
    /// This is the most critical query and uses the composite index for sub-ms performance.
    /// </summary>
    [HttpGet("session")]
    [ProducesResponseType(typeof(ReviewSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSession([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetReviewSessionQuery(limit), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Submit review result for a flashcard. Triggers SM-2 recalculation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitReview([FromBody] SubmitReviewRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new SubmitReviewCommand(request.UserVocabularyId, request.QualityScore, request.TimeSpentMs), ct);
        return ToActionResult(result);
    }

    /// <summary>Get review history with pagination.</summary>
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

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
