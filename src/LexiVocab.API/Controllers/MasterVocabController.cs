using LexiVocab.Application.Common;
using LexiVocab.Application.Features.MasterVocabularies.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/v1/master-vocab")]
[Produces("application/json")]
public class MasterVocabController : ControllerBase
{
    private readonly IMediator _mediator;

    public MasterVocabController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lookup a word in the master dictionary. Returns phonetics, audio URL, etc.
    /// Public endpoint — no auth required (used by Extension for quick lookups).
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string word, CancellationToken ct)
    {
        var result = await _mediator.Send(new LookupMasterVocabQuery(word), ct);
        return ToActionResult(result);
    }

    /// <summary>Search master dictionary with prefix matching (for autocomplete).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchMasterVocabQuery(q, limit), ct);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
