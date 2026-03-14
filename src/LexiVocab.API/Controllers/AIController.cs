using LexiVocab.Application.Common;
using LexiVocab.Application.Features.AI;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

using Asp.Versioning;

namespace LexiVocab.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ai")]
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

    /// <summary>
    /// Professional streaming endpoint for word explanation.
    /// Uses Server-Sent Events (SSE).
    /// </summary>
    [HttpGet("explain-stream")]
    public async Task GetExplainStream([FromQuery] string word, [FromQuery] string? context, CancellationToken ct, [FromQuery] bool asJson = false)
    {
        var result = await _mediator.Send(new StreamWordExplanationQuery(word, context, asJson), ct);

        if (!result.IsSuccess)
        {
            Response.StatusCode = result.StatusCode;
            Response.ContentType = "application/json";
            await Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = result.Error }), ct);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var stream = result.Data!;

        try
        {
            // 1. Send metadata/start signal
            var startMetadata = JsonSerializer.Serialize(new { type = "start", word, timestamp = DateTime.UtcNow });
            await Response.WriteAsync($"data: {startMetadata}\n\n", ct);
            await Response.Body.FlushAsync(ct);

            await foreach (var chunk in stream.WithCancellation(ct))
            {
                // 2. Send content chunks as JSON
                var contentJson = JsonSerializer.Serialize(new { type = "content", delta = chunk });
                await Response.WriteAsync($"data: {contentJson}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            
            // 3. Send final done signal
            var doneJson = JsonSerializer.Serialize(new { type = "done" });
            await Response.WriteAsync($"data: {doneJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        catch (Exception ex)
        {
            var errorJson = JsonSerializer.Serialize(new { type = "error", message = ex.Message });
            await Response.WriteAsync($"data: {errorJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
