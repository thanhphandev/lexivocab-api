using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Queries;

/// <summary>
/// Query audit logs with filtering and pagination.
/// Previously handled inline in AdminController — moved here to comply with Clean Architecture.
/// </summary>
public record GetAuditLogsQuery(
    Guid? UserId = null,
    AuditAction? Action = null,
    string? EntityType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50)
    : IRequest<Result<PagedResult<AuditLogDto>>>;

public class GetAuditLogsHandler : IRequestHandler<GetAuditLogsQuery, Result<PagedResult<AuditLogDto>>>
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditLogsHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result<PagedResult<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _auditLogRepository.GetPagedAsync(
            request.UserId, request.Action, request.EntityType,
            request.FromDate, request.ToDate, request.Search,
            request.Page, request.PageSize, ct);

        var dtos = items.Select(a => new AuditLogDto(
            a.Id,
            a.UserId,
            a.UserEmail,
            a.Action.ToString(),
            a.EntityType,
            a.EntityId,
            a.OldValues,
            a.NewValues,
            a.IpAddress,
            a.UserAgent,
            a.RequestName,
            a.TraceId,
            a.AdditionalInfo,
            a.IsSuccess,
            a.DurationMs,
            a.Timestamp)).ToList();

        return Result<PagedResult<AuditLogDto>>.Success(new PagedResult<AuditLogDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
