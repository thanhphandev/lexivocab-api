using LexiVocab.Application.Features.MasterVocabularies.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Asp.Versioning;

namespace LexiVocab.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/master-vocab")]
[Produces("application/json")]
public class MasterVocabController : BaseApiController
{
    private readonly IMediator _mediator;

    public MasterVocabController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lookup word in master dictionary.
    /// </summary>
    /// <remarks>
    /// Public endpoint (no auth required). Returns phonetics, audio URL, and standard meanings.
    /// Used by the Chrome Extension for instant tooltips.
    /// </remarks>
    /// <param name="word">Word to lookup.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns dictionary data.</response>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string word, CancellationToken ct)
    {
        var result = await _mediator.Send(new LookupMasterVocabQuery(word), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Search master dictionary.
    /// </summary>
    /// <remarks>
    /// Prefix matching for autocomplete functionality.
    /// </remarks>
    /// <param name="q">Search query.</param>
    /// <param name="limit">Max results (default: 10).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns matching words.</response>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchMasterVocabQuery(q, limit), ct);
        return ToActionResult(result);
    }

}
