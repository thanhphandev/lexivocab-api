using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Tag;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Application.Features.Tags.Commands;
using LexiVocab.Application.Features.Tags.Queries;
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
public class TagsController : BaseApiController
{
    private readonly IMediator _mediator;

    public TagsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Get all user tags.
    /// </summary>
    /// <remarks>
    /// Returns a list of all tags created by the user, including the word count for each tag.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns list of tags.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTagListQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get vocabularies by tag.
    /// </summary>
    /// <param name="id">Tag ID.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns paginated vocabularies.</response>
    [HttpGet("{id:guid}/vocabularies")]
    [ProducesResponseType(typeof(PagedResult<VocabularyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVocabularies(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTagVocabulariesQuery(id, page, pageSize), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Create a new tag.
    /// </summary>
    /// <param name="request">Tag data (Name, Color, Icon).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Returns the created tag.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateTagCommand(request.Name, request.Color, request.Icon), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Update tag details.
    /// </summary>
    /// <param name="id">Tag ID.</param>
    /// <param name="request">Updated tag data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the updated tag.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTagRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTagCommand(id, request.Name, request.Color, request.Icon, request.DisplayOrder), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Delete a tag.
    /// </summary>
    /// <remarks>
    /// Vocabularies associated with this tag will remain but will be marked as 'Uncategorized'.
    /// </remarks>
    /// <param name="id">Tag ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Tag deleted.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteTagCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Assign vocabulary to tag.
    /// </summary>
    /// <param name="id">Tag ID.</param>
    /// <param name="vocabularyId">Vocabulary ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Word assigned to tag.</response>
    [HttpPatch("{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignWord(Guid id, [FromBody] Guid vocabularyId, CancellationToken ct)
    {
        var result = await _mediator.Send(new AssignVocabToTagCommand(id, vocabularyId), ct);
        return ToActionResult(result);
    }
}
