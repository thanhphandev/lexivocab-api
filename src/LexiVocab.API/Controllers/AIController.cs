using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.AI;
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
public class AIController : BaseApiController
{
    private readonly IMediator _mediator;

    public AIController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Suggest synonyms, antonyms, and collocations for a word.
    /// </summary>
    /// <remarks>
    /// This endpoint uses AI to find semantically related words. 
    /// Requires an active **Premium** subscription.
    /// </remarks>
    /// <param name="word">The word to find related terms for.</param>
    /// <param name="targetLanguage">Optional: Target language for translations (e.g., 'English'). Defaults to user settings.</param>
    /// <param name="userLanguage">Optional: User's native language (e.g., 'Vietnamese'). Defaults to user settings.</param>
    /// <param name="provider">Optional: The AI provider to use.</param>
    /// <param name="modelId">Optional: The specific model ID to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns a list of synonyms, antonyms, and collocations.</response>
    /// <response code="401">Unauthorized: User is not authenticated.</response>
    /// <response code="403">Forbidden: User does not have a Pro plan or quota exceeded.</response>
    [HttpGet("related/{word}")]
    [ProducesResponseType(typeof(RelatedWordsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRelated(string word, [FromQuery] string? targetLanguage, [FromQuery] string? userLanguage, [FromQuery] string? provider, [FromQuery] string? modelId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRelatedWordsQuery(word, targetLanguage, userLanguage, provider, modelId), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Generate a multiple-choice quiz for a specific word.
    /// </summary>
    /// <remarks>
    /// Creates a 4-option quiz based on the word's meaning and context.
    /// Requires an active **Premium** subscription.
    /// </remarks>
    /// <param name="word">The word to generate a quiz for.</param>
    /// <param name="targetLanguage">Optional: Target language. Defaults to user settings.</param>
    /// <param name="userLanguage">Optional: User language. Defaults to user settings.</param>
    /// <param name="provider">Optional: The AI provider to use.</param>
    /// <param name="modelId">Optional: The specific model ID to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the generated quiz object.</response>
    /// <response code="403">Forbidden: Pro plan required or daily limit reached.</response>
    [HttpGet("quiz/{word}")]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuiz(string word, [FromQuery] string? targetLanguage, [FromQuery] string? userLanguage, [FromQuery] string? provider, [FromQuery] string? modelId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWordQuizQuery(word, targetLanguage, userLanguage, provider, modelId), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Professional streaming endpoint for word explanation (SSE).
    /// </summary>
    /// <remarks>
    /// Uses Server-Sent Events (SSE) to stream a detailed explanation of the word, including usage examples.
    /// Format: `data: {"type": "content", "delta": "..."}`.
    /// </remarks>
    /// <param name="word">The word to explain.</param>
    /// <param name="context">Optional: The context sentence where the word appeared.</param>
    /// <param name="targetLanguage">Optional: Target language. Defaults to user settings.</param>
    /// <param name="userLanguage">Optional: User language. Defaults to user settings.</param>
    /// <param name="provider">Optional: The AI provider to use.</param>
    /// <param name="modelId">Optional: The specific model ID to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="asJson">If true, requests the AI to output a structured JSON explanation.</param>
    /// <response code="200">Streams SSE events.</response>
    /// <response code="403">Forbidden: Daily AI request limit reached.</response>
    [HttpGet("explain-stream")]
    [Produces("text/event-stream")]
    public async Task GetExplainStream([FromQuery] string word, [FromQuery] string? context, [FromQuery] string? targetLanguage, [FromQuery] string? userLanguage, [FromQuery] string? provider, [FromQuery] string? modelId, CancellationToken ct, [FromQuery] bool asJson = false)
    {
        var result = await _mediator.Send(new StreamWordExplanationQuery(word, context, asJson, targetLanguage, userLanguage, provider, modelId), ct);

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
    /// Streams a short story containing the specific word.
    /// </summary>
    [HttpGet("story-stream")]
    [Produces("text/event-stream")]
    public async Task GetStoryStream([FromQuery] string word, [FromQuery] string? targetLanguage, [FromQuery] string? userLanguage, [FromQuery] string? provider, [FromQuery] string? modelId, CancellationToken ct)
    {
        var result = await _mediator.Send(new StreamStoryQuery(word, targetLanguage, userLanguage, provider, modelId), ct);

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


    /// <summary>
    /// Translates a word instantly (non-streaming).
    /// </summary>
    /// <remarks>
    /// Supports multiple providers (Google, Bing, Lingva, etc.) and LLM-based translations.
    /// Free tier uses Google/Lingva by default. Premium tier can use Advanced LLMs.
    /// </remarks>
    /// <param name="word">Word to translate.</param>
    /// <param name="context">Optional: Context sentence.</param>
    /// <param name="provider">Optional: Translation provider (e.g., 'google', 'cloudflare', 'custom').</param>
    /// <param name="modelId">Optional: Model identifier for LLM providers.</param>
    /// <param name="from">Optional: Source language code (e.g., 'en').</param>
    /// <param name="to">Optional: Target language code (e.g., 'vi').</param>
    /// <param name="customBaseUrl">Advanced: Override API base URL.</param>
    /// <param name="customApiKey">Advanced: User-provided API Key.</param>
    /// <param name="customModel">Advanced: User-provided model name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the raw JSON translation string.</response>
    /// <response code="403">Forbidden: Quota exceeded for the selected provider.</response>
    /// <response code="502">Bad Gateway: Error from upstream translation provider.</response>
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
    /// Streams translation using the specified LLM Model (SSE).
    /// </summary>
    /// <remarks>
    /// Streams the JSON translation object chunk by chunk. 
    /// Best for long contexts or when using LLMs for high-quality translation.
    /// </remarks>
    /// <param name="word">Word to translate.</param>
    /// <param name="context">Optional: Context sentence.</param>
    /// <param name="provider">Optional: Provider ID.</param>
    /// <param name="modelId">Optional: Model ID.</param>
    /// <param name="from">Optional: Source language.</param>
    /// <param name="to">Optional: Target language.</param>
    /// <param name="customBaseUrl">Advanced: Base URL override.</param>
    /// <param name="customApiKey">Advanced: API Key override.</param>
    /// <param name="customModel">Advanced: Model name override.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Streams translation chunks via SSE.</response>
    /// <response code="403">Forbidden: Plan limit reached.</response>
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

    /// <summary>
    /// Streams input translation using the specified Style and LLM Model (SSE) via POST.
    /// </summary>
    /// <param name="request">The input translation request body.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("input-translate-stream")]
    [Produces("text/event-stream")]
    public async Task PostInputTranslateStream([FromBody] StreamInputTranslateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new StreamInputTranslateQuery(
            request.Text, request.TargetLanguage, request.Style, request.Provider, request.ModelId, request.CustomBaseUrl, request.CustomApiKey, request.CustomModel), ct);

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
            var startMetadata = JsonSerializer.Serialize(new { type = "start", timestamp = DateTime.UtcNow });
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
}
