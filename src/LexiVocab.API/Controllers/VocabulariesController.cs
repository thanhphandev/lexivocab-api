using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Application.Features.Vocabularies.Commands;
using LexiVocab.Application.Features.Vocabularies.Queries;
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
public class VocabulariesController : BaseApiController
{
    private readonly IMediator _mediator;

    public VocabulariesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Get paginated vocabulary list.
    /// </summary>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100).</param>
    /// <param name="isArchived">Optional filter by archive status.</param>
    /// <param name="search">Optional search term for word text or meaning.</param>
    /// <param name="tagId">Optional filter by tag ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns a paginated list of vocabulary cards.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VocabularyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isArchived = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? tagId = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var result = await _mediator.Send(
            new GetVocabularyListQuery(page, pageSize, isArchived, search, tagId), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Explore community-approved master vocabularies.
    /// </summary>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="search">Search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns list of master vocabulary entries.</response>
    [HttpGet("explore")]
    [ProducesResponseType(typeof(PagedResult<LexiVocab.Application.DTOs.MasterVocabulary.MasterVocabularyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Explore(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var result = await _mediator.Send(new ExploreVocabulariesQuery(page, pageSize, search), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get vocabulary card details.
    /// </summary>
    /// <param name="id">The unique identifier of the vocabulary card.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the vocabulary card details.</response>
    /// <response code="404">Not Found: Card does not exist or belongs to another user.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VocabularyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVocabularyByIdQuery(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Create a new personal vocabulary card.
    /// </summary>
    /// <remarks>
    /// Used by the Chrome Extension to save words discovered while browsing.
    /// Automatically links to master dictionary if the word exists.
    /// </remarks>
    /// <param name="request">Word data (text, meaning, context, source URL).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Created: Returns the new vocabulary card.</response>
    /// <response code="403">Forbidden: Word limit reached for current plan.</response>
    /// <response code="409">Conflict: Word already exists in user's list.</response>
    [HttpPost]
    [ProducesResponseType(typeof(VocabularyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateVocabularyRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateVocabularyCommand(request.WordText, request.CustomMeaning,
                request.ContextSentence, request.SourceUrl, request.TagId), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Update vocabulary card.
    /// </summary>
    /// <param name="id">Vocabulary ID.</param>
    /// <param name="request">Updated meaning or context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the updated card.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(VocabularyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVocabularyRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateVocabularyCommand(id, request.CustomMeaning, request.ContextSentence), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Change the tag of a vocabulary card.
    /// </summary>
    /// <param name="id">Vocabulary ID.</param>
    /// <param name="request">New Tag ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Tag updated.</response>
    [HttpPatch("{id:guid}/tag")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTag(Guid id, [FromBody] UpdateVocabularyTagRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateVocabularyTagCommand(id, request.TagId), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Toggle archive/mastered status.
    /// </summary>
    /// <remarks>
    /// Archived words are hidden from active review sessions.
    /// </remarks>
    /// <param name="id">Vocabulary ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Status toggled.</response>
    [HttpPatch("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ArchiveVocabularyCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Permanently delete a vocabulary card.
    /// </summary>
    /// <param name="id">Vocabulary ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Card deleted.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteVocabularyCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Contribute to Master Vocabulary.
    /// </summary>
    /// <remarks>
    /// Submits your word to the public master list for moderation.
    /// </remarks>
    /// <param name="id">Vocabulary ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Contribution submitted.</response>
    [HttpPost("{id:guid}/contribute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ContributeToMaster(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ContributeToMasterCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Bulk import vocabulary words.
    /// </summary>
    /// <remarks>
    /// Requires **Premium** subscription.
    /// </remarks>
    /// <param name="request">List of words to import.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Returns the number of words successfully imported.</response>
    /// <response code="403">Forbidden: Premium plan required.</response>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BatchImport([FromBody] BatchImportRequest request, CancellationToken ct)
    {
        var commands = request.Words.Select(w =>
            new CreateVocabularyCommand(w.WordText, w.CustomMeaning, w.ContextSentence, w.SourceUrl, w.TagId)).ToList();
        var result = await _mediator.Send(new BatchImportCommand(commands), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get vocabulary statistics.
    /// </summary>
    /// <remarks>
    /// Returns counts for total, active, archived, and due today words.
    /// </remarks>
    /// <response code="200">Returns statistics.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(VocabularyStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVocabularyStatsQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Export vocabulary data.
    /// </summary>
    /// <remarks>
    /// Requires **Premium** subscription. Supports JSON, CSV, and Quizlet formats.
    /// </remarks>
    /// <param name="format">Export format ('json', 'csv', 'quizlet', 'txt').</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the file for download.</response>
    /// <response code="403">Forbidden: Premium plan required.</response>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export([FromQuery] string format = "json", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ExportVocabulariesQuery(format), ct);
        if (result.IsSuccess)
            return File(result.Data!.Bytes, result.Data.ContentType, result.Data.FileName);

        return ToActionResult(result);
    }
}
