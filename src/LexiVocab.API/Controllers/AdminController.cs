using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Application.Features.Admin.Commands;
using LexiVocab.Application.Features.Admin.Queries;
using LexiVocab.Application.Features.Admin.Features.Commands;
using LexiVocab.Application.Features.Admin.Features.Queries;
using LexiVocab.Application.Features.Admin.Plans.Commands;
using LexiVocab.Application.Features.Admin.Plans.Queries;
using LexiVocab.Application.Features.Admin.Vocabularies.Commands;
using LexiVocab.Application.Features.Admin.Vocabularies.Queries;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Asp.Versioning;
using LexiVocab.Application.DTOs.Auth;

namespace LexiVocab.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminController : BaseApiController
{
    private readonly IMediator _mediator;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminController(IMediator mediator, IAuditLogRepository auditLogRepository)
    {
        _mediator = mediator;
        _auditLogRepository = auditLogRepository;
    }

    /// <summary>
    /// Get all users.
    /// </summary>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="search">Search by email or name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns paginated user list.</response>
    [HttpGet("users")]
    [ProducesResponseType(typeof(PagedResult<UserOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetUsersQuery(page, pageSize, search), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get user details.
    /// </summary>
    /// <param name="id">User ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns full user profile and stats.</response>
    [HttpGet("users/{id}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserDetailQuery(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Update user role.
    /// </summary>
    /// <param name="id">User ID.</param>
    /// <param name="request">New role (User/Admin).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Role updated.</response>
    [HttpPut("users/{id}/role")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateUserRoleCommand(id, request.Role), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Update user active status.
    /// </summary>
    /// <param name="id">User ID.</param>
    /// <param name="request">Active status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Status updated.</response>
    [HttpPut("users/{id}/status")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateUserStatusCommand(id, request.IsActive), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Impersonate a user for debugging (Generates a 15-minute token).
    /// </summary>
    /// <param name="id">Target User ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns Auth response with impersonation JWT.</response>
    [HttpPost("users/{id}/impersonate")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImpersonateUser(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ImpersonateUserCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Manually add subscription.
    /// </summary>
    /// <param name="id">User ID.</param>
    /// <param name="request">Plan and duration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Subscription added.</response>
    [HttpPost("users/{id}/subscriptions")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddSubscription(Guid id, [FromBody] AddSubscriptionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddManualSubscriptionCommand(id, request.Plan, request.DurationDays), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Force cancel user subscription.
    /// </summary>
    /// <param name="id">User ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Subscription cancelled.</response>
    [HttpDelete("users/{id}/subscriptions")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelSubscription(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelCurrentSubscriptionCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get basic system metrics.
    /// </summary>
    /// <response code="200">Returns counts for users, vocab, and revenue.</response>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(SystemStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemMetrics(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSystemStatsQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get advanced analytics.
    /// </summary>
    /// <response code="200">Returns DAU, Churn, and Engagement metrics.</response>
    [HttpGet("metrics/advanced")]
    [ProducesResponseType(typeof(AdvancedSystemStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdvancedSystemMetrics(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAdvancedSystemStatsQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Query system audit logs.
    /// </summary>
    /// <param name="userId">Filter by User ID.</param>
    /// <param name="action">Filter by Action type.</param>
    /// <param name="entityType">Filter by Entity type.</param>
    /// <param name="fromDate">Start date.</param>
    /// <param name="toDate">End date.</param>
    /// <param name="search">Search text.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns paginated audit logs.</response>
    [HttpGet("audit-logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid? userId = null,
        [FromQuery] AuditAction? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        // Clamp page size to prevent excessive data retrieval
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var (items, totalCount) = await _auditLogRepository.GetPagedAsync(
            userId, action, entityType, fromDate, toDate, search, page, pageSize, ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                items = items.Select(a => new
                {
                    a.Id,
                    a.UserId,
                    a.UserEmail,
                    action = a.Action.ToString(),
                    a.EntityType,
                    a.EntityId,
                    oldValues = a.OldValues,
                    newValues = a.NewValues,
                    a.IpAddress,
                    a.UserAgent,
                    a.RequestName,
                    a.TraceId,
                    a.AdditionalInfo,
                    a.IsSuccess,
                    a.DurationMs,
                    a.Timestamp
                }),
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        });
    }

    // ─── Feature Definitions ────────────────────────────────

    /// <summary>Create a new feature definition.</summary>
    [HttpPost("features/definitions")]
    [ProducesResponseType(typeof(FeatureDefinitionDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateFeatureDefinition([FromBody] CreateFeatureDefinitionCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Update an existing feature definition.</summary>
    [HttpPut("features/definitions/{id}")]
    [ProducesResponseType(typeof(FeatureDefinitionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFeatureDefinition(Guid id, [FromBody] UpdateFeatureDefinitionRequest request, CancellationToken ct)
    {
        var command = new UpdateFeatureDefinitionCommand(
            id,
            request.Description,
            request.ValueType,
            request.DefaultValue);

        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Delete a feature definition.</summary>
    [HttpDelete("features/definitions/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteFeatureDefinition(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteFeatureDefinitionCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Get a feature definition by ID.</summary>
    [HttpGet("features/definitions/{id}")]
    [ProducesResponseType(typeof(FeatureDefinitionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeatureDefinition(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFeatureDefinitionByIdQuery(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Get all feature definitions.</summary>
    [HttpGet("features/definitions")]
    [ProducesResponseType(typeof(List<FeatureDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeatureDefinitions(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFeatureDefinitionsQuery(), ct);
        return ToActionResult(result);
    }

    // ─── Plan Definitions ───────────────────────────────────

    /// <summary>Create a new plan definition.</summary>
    [HttpPost("plans/definitions")]
    [ProducesResponseType(typeof(PlanDefinitionDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePlanDefinition([FromBody] CreatePlanDefinitionCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Update an existing plan definition.</summary>
    [HttpPut("plans/definitions/{id}")]
    [ProducesResponseType(typeof(PlanDefinitionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePlanDefinition(Guid id, [FromBody] UpdatePlanDefinitionRequest request, CancellationToken ct)
    {
        var command = new UpdatePlanDefinitionCommand(
            id,
            request.Name,
            request.IsActive,
            request.Features,
            request.Pricings);

        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Delete a plan definition.</summary>
    [HttpDelete("plans/definitions/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePlanDefinition(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeletePlanDefinitionCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Get a plan definition by ID.</summary>
    [HttpGet("plans/definitions/{id}")]
    [ProducesResponseType(typeof(PlanDefinitionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanDefinition(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPlanDefinitionByIdQuery(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Get all plan definitions.</summary>
    [HttpGet("plans/definitions")]
    [ProducesResponseType(typeof(List<PlanDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanDefinitions(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPlanDefinitionsQuery(), ct);
        return ToActionResult(result);
    }

    // ─── Master Vocabularies ────────────────────────────────

    /// <summary>Create a new master vocabulary word manually.</summary>
    [HttpPost("vocabularies/master")]
    [ProducesResponseType(typeof(MasterVocabularyDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMasterVocabulary([FromBody] CreateMasterVocabularyCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Batch import master vocabulary words.</summary>
    [HttpPost("vocabularies/master/batch")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMasterVocabularyBatch([FromBody] CreateMasterVocabularyBatchCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Look up a word from an external dictionary.</summary>
    [HttpGet("vocabularies/master/lookup")]
    [ProducesResponseType(typeof(MasterVocabularyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> LookupMasterVocabularyFromDictionary([FromQuery] string word, CancellationToken ct)
    {
        var result = await _mediator.Send(new SuggestMasterVocabFromDictionaryQuery(word), ct);
        return ToActionResult(result);
    }

    /// <summary>Update an existing master vocabulary word.</summary>
    [HttpPut("vocabularies/master/{id}")]
    [ProducesResponseType(typeof(MasterVocabularyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMasterVocabulary(Guid id, [FromBody] UpdateMasterVocabularyCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest(new { success = false, error = "Path ID and Body ID mismatch." });

        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Approve a community-contributed master vocabulary word.</summary>
    [HttpPatch("vocabularies/master/{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveMasterVocabulary(Guid id, [FromBody] ApproveMasterVocabularyCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest(new { success = false, error = "Path ID and Body ID mismatch." });

        var result = await _mediator.Send(command, ct);
        return ToActionResult(result);
    }

    /// <summary>Delete a master vocabulary word.</summary>
    [HttpDelete("vocabularies/master/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMasterVocabulary(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteMasterVocabularyCommand(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Get a master vocabulary word by ID.</summary>
    [HttpGet("vocabularies/master/{id}")]
    [ProducesResponseType(typeof(MasterVocabularyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMasterVocabulary(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMasterVocabularyByIdQuery(id), ct);
        return ToActionResult(result);
    }

    /// <summary>Get paginated list of master vocabularies.</summary>
    [HttpGet("vocabularies/master")]
    [ProducesResponseType(typeof(PagedResult<MasterVocabularyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMasterVocabularies(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isApproved = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMasterVocabulariesQuery(page, pageSize, search, isApproved), ct);
        return ToActionResult(result);
    }

}
