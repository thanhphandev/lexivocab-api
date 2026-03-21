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
    /// Suggest synonyms, antonyms, and collocations for a word.
    /// Requires PRO plan.
    /// </summary>
    [HttpGet("related/{word}")]
    public async Task<IActionResult> GetRelated(string word, [FromQuery] string? targetLanguage, [FromQuery] string? userLanguage, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRelatedWordsQuery(word, targetLanguage, userLanguage), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Generate a multiple-choice quiz for a word.
    /// Requires PRO plan.
    /// </summary>
    [HttpGet("quiz/{word}")]
    public async Task<IActionResult> GetQuiz(string word, [FromQuery] string? targetLanguage, [FromQuery] string? userLanguage, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWordQuizQuery(word, targetLanguage, userLanguage), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Professional streaming endpoint for word explanation.
    /// Uses Server-Sent Events (SSE).
    /// </summary>
    [HttpGet("explain-stream")]
    [Produces("text/event-stream")]
    public async Task GetExplainStream([FromQuery] string word, [FromQuery] string? context, [FromQuery] string? targetLanguage, [FromQuery] string? userLanguage, CancellationToken ct, [FromQuery] bool asJson = false)
    {
        var result = await _mediator.Send(new StreamWordExplanationQuery(word, context, asJson, targetLanguage, userLanguage), ct);

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
        Response.Headers["X-Accel-Buffering"] = "no"; // Disable Nginx buffering
        
        await Response.Body.FlushAsync(ct); // Send headers immediately

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

    /// <summary>
    /// Translates a word instantly (non-streaming).
    /// </summary>
    [HttpGet("translate")]
    [Produces("application/json")]
    public async Task<IActionResult> GetTranslate([FromQuery] string word, [FromQuery] string? context, [FromQuery] string? provider, [FromQuery] string? modelId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? customBaseUrl, [FromQuery] string? customApiKey, [FromQuery] string? customModel, CancellationToken ct)
    {
        var result = await _mediator.Send(new TranslateQuery(word, context, provider, modelId, from, to, customBaseUrl, customApiKey, customModel), ct);

        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, new { success = false, error = result.Error });
        }
        
        if (result.Data != null && result.Data.TrimStart().StartsWith("{\"error\""))
        {
            return StatusCode(502, result.Data); // 502 Bad Gateway mapping for upstream errors
        }
        
        return Content(result.Data!, "application/json");
    }

    /// <summary>
    /// Streams translation using the specified LLM Model.
    /// </summary>
    [HttpGet("translate-stream")]
    [Produces("text/event-stream")]
    public async Task GetTranslateStream([FromQuery] string word, [FromQuery] string? context, [FromQuery] string? provider, [FromQuery] string? modelId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? customBaseUrl, [FromQuery] string? customApiKey, [FromQuery] string? customModel, CancellationToken ct)
    {
        var result = await _mediator.Send(new StreamTranslateQuery(word, context, provider, modelId, from, to, customBaseUrl, customApiKey, customModel), ct);

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
        Response.Headers["X-Accel-Buffering"] = "no";
        
        await Response.Body.FlushAsync(ct);

        var stream = result.Data!;

        try
        {
            var startMetadata = JsonSerializer.Serialize(new { type = "start", word, timestamp = DateTime.UtcNow });
            await Response.WriteAsync($"data: {startMetadata}\n\n", ct);
            await Response.Body.FlushAsync(ct);

            await foreach (var chunk in stream.WithCancellation(ct))
            {
                if (chunk.TrimStart().StartsWith("{\"error\""))
                {
                    try {
                        using var doc = JsonDocument.Parse(chunk);
                        if (doc.RootElement.TryGetProperty("error", out var errorProp))
                        {
                            var errMsg = errorProp.GetString() ?? chunk;
                            var errRespJson = JsonSerializer.Serialize(new { type = "error", message = errMsg });
                            await Response.WriteAsync($"data: {errRespJson}\n\n", ct);
                            await Response.Body.FlushAsync(ct);
                            continue;
                        }
                    } catch { } // Not a valid json error, fallback to content
                }

                var contentJson = JsonSerializer.Serialize(new { type = "content", delta = chunk });
                await Response.WriteAsync($"data: {contentJson}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            
            var doneJson = JsonSerializer.Serialize(new { type = "done" });
            await Response.WriteAsync($"data: {doneJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException) { }
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
