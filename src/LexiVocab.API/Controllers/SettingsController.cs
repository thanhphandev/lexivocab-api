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
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get current user's extension settings.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSettingsQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>Update extension settings. Supports partial updates.</summary>
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

    /// <summary>Tests Telegram and Zalo bot notification configurations.</summary>
    [HttpPost("test-bot-notifications")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestBotNotifications([FromBody] TestBotSettingsCommand payload, CancellationToken ct)
    {
        var result = await _mediator.Send(payload, ct);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
