using LexiVocab.Application.Common;
using LexiVocab.Application.Features.AI;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/ai")]
[Produces("application/json")]
public class AIController : ControllerBase
{
    private readonly IMediator _mediator;

    public AIController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Explain the usage and nuances of a word, optionally in context.
    /// Requires PRO plan.
    /// </summary>
    [HttpPost("explain")]
    public async Task<IActionResult> Explain([FromBody] GetWordExplanationQuery query, CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Suggest synonyms, antonyms, and collocations for a word.
    /// Requires PRO plan.
    /// </summary>
    [HttpGet("related/{word}")]
    public async Task<IActionResult> GetRelated(string word, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRelatedWordsQuery(word), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Generate a multiple-choice quiz for a word.
    /// Requires PRO plan.
    /// </summary>
    [HttpGet("quiz/{word}")]
    public async Task<IActionResult> GetQuiz(string word, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWordQuizQuery(word), ct);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
