using LexiVocab.Application.Common;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/master-vocab")]
[Produces("application/json")]
public class MasterVocabController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public MasterVocabController(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Lookup a word in the master dictionary. Returns phonetics, audio URL, etc.
    /// Public endpoint — no auth required (used by Extension for quick lookups).
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string word, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(word))
            return BadRequest(new { success = false, error = "Word parameter is required." });

        var result = await _uow.MasterVocabularies.GetByWordAsync(word.ToLowerInvariant().Trim(), ct);
        if (result is null)
            return NotFound(new { success = false, error = $"Word '{word}' not found in master dictionary." });

        return Ok(new
        {
            success = true,
            data = new
            {
                result.Word,
                result.PartOfSpeech,
                result.PhoneticUk,
                result.PhoneticUs,
                result.AudioUrl,
                result.PopularityRank
            }
        });
    }

    /// <summary>Search master dictionary with prefix matching (for autocomplete).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { success = false, error = "Query parameter 'q' is required." });

        var results = await _uow.MasterVocabularies.SearchAsync(q.ToLowerInvariant().Trim(), limit, ct);

        return Ok(new
        {
            success = true,
            data = results.Select(r => new
            {
                r.Word,
                r.PartOfSpeech,
                r.PhoneticUs,
                r.PopularityRank
            })
        });
    }
}
