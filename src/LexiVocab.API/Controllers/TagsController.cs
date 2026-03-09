using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Tag;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Application.Features.Tags.Commands;
using LexiVocab.Application.Features.Tags.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class TagsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TagsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get list of all user tags with their word counts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTagListQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>Get paginated vocabularies belonging to a specific tag.</summary>
    [HttpGet("{id:guid}/vocabularies")]
    [ProducesResponseType(typeof(PagedResult<VocabularyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVocabularies(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTagVocabulariesQuery(id, page, pageSize), ct);
        return ToActionResult(result);
    }

    /// <summary>Create a new custom tag.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateTagCommand(request.Name, request.Color, request.Icon), ct);
        return ToActionResult(result);
    }

    /// <summary>Update an existing tag details.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTagRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTagCommand(id, request.Name, request.Color, request.Icon, request.DisplayOrder), ct);
        return ToActionResult(result);
    }

    /// <summary>Delete a tag (vocabularies will remain as Uncategorized).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteTagCommand(id), ct);
        if (result.IsSuccess)
            return Ok(new { success = true });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }

    /// <summary>Assign a vocabulary to this tag.</summary>
    [HttpPatch("{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignWord(Guid id, [FromBody] Guid vocabularyId, CancellationToken ct)
    {
        var result = await _mediator.Send(new AssignVocabToTagCommand(id, vocabularyId), ct);
        if (result.IsSuccess)
            return Ok(new { success = true });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
