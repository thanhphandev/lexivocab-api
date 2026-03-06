using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Application.Features.Vocabularies.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class VocabulariesController : ControllerBase
{
    private readonly IMediator _mediator;

    public VocabulariesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get paginated vocabulary list with optional search and archive filter.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VocabularyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isArchived = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var result = await _mediator.Send(
            new GetVocabularyListQuery(page, pageSize, isArchived, search), ct);
        return ToActionResult(result);
    }

    /// <summary>Get a single vocabulary card by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VocabularyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVocabularyByIdQuery(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Save a new vocabulary word (typically from the Chrome Extension).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(VocabularyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateVocabularyRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateVocabularyCommand(request.WordText, request.CustomMeaning,
                request.ContextSentence, request.SourceUrl), ct);
        return ToActionResult(result);
    }

    /// <summary>Update meaning or context of a vocabulary card.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(VocabularyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVocabularyRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateVocabularyCommand(id, request.CustomMeaning, request.ContextSentence), ct);
        return ToActionResult(result);
    }

    /// <summary>Toggle archive status (soft delete / mark as mastered).</summary>
    [HttpPatch("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ArchiveVocabularyCommand(id), ct);
        if (result.IsSuccess)
            return Ok(new { success = true });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }

    /// <summary>Permanently delete a vocabulary card and all its review history.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteVocabularyCommand(id), ct);
        if (result.IsSuccess)
            return Ok(new { success = true });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }

    /// <summary>Batch import multiple words at once.</summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<IActionResult> BatchImport([FromBody] BatchImportRequest request, CancellationToken ct)
    {
        var commands = request.Words.Select(w =>
            new CreateVocabularyCommand(w.WordText, w.CustomMeaning, w.ContextSentence, w.SourceUrl)).ToList();
        var result = await _mediator.Send(new BatchImportCommand(commands), ct);
        return ToActionResult(result);
    }

    /// <summary>Get vocabulary statistics (total, active, archived, due today).</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(VocabularyStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVocabularyStatsQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>Export vocabulary data to CSV or JSON (Premium Only).</summary>
    [HttpGet("export")]
    [Authorize(Policy = "RequirePremium")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export([FromQuery] string format = "json", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ExportVocabulariesQuery(format), ct);
        if (result.IsSuccess)
            return File(result.Data!.Bytes, result.Data.ContentType, result.Data.FileName);

        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
