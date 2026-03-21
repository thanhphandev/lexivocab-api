using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Settings;
using LexiVocab.Application.Features.Settings;
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
public class SettingsController : BaseApiController
{
    private readonly IMediator _mediator;

    public SettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Get user settings.
    /// </summary>
    /// <remarks>
    /// Returns the extension settings, including UI preferences and notification configs.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns settings.</response>
    [HttpGet]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSettingsQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Update user settings.
    /// </summary>
    /// <remarks>
    /// Supports partial updates for any setting field.
    /// </remarks>
    /// <param name="request">New settings data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns updated settings.</response>
    [HttpPut]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateSettingsCommand(
                request.IsHighlightEnabled,
                request.HighlightColor,
                request.ExcludedDomains,
                request.DailyGoal,
                request.DailyNewCardLimit,
                request.DailyReviewLimit,
                request.PreferencesJson,
                request.TargetLanguage,
                request.NativeLanguage,
                request.CustomLlmsJson,
                request.DefaultTranslator,
                request.IsEmailReminderEnabled,
                request.IsTelegramReminderEnabled,
                request.TelegramBotToken,
                request.TelegramChatId,
                request.IsZaloReminderEnabled,
                request.ZaloBotToken,
                request.ZaloUserId), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Test notification settings.
    /// </summary>
    /// <remarks>
    /// Sends a test message to the configured Telegram or Zalo bot.
    /// </remarks>
    /// <param name="payload">Bot configuration to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns true if test message was sent successfully.</response>
    [HttpPost("test-bot-notifications")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestBotNotifications([FromBody] TestBotSettingsCommand payload, CancellationToken ct)
    {
        var result = await _mediator.Send(payload, ct);
        return ToActionResult(result);
    }

}
