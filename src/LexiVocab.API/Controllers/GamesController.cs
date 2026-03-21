using Asp.Versioning;
using LexiVocab.Application.DTOs.Games;
using LexiVocab.Application.Features.Games;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class GamesController : ControllerBase
{
    private readonly IMediator _mediator;

    public GamesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("hub")]
    public async Task<IActionResult> GetGameHubData(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGameHubDataQuery(), ct);
        return ToActionResult(result);
    }

    [HttpGet("session")]
    public async Task<IActionResult> GetSession([FromQuery] string mode, [FromQuery] Guid? deckId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGameSessionDataQuery(mode ?? "personal", deckId), ct);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(LexiVocab.Application.Common.Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
