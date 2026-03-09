using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;
using System.Text.Json;

namespace LexiVocab.Application.Features.Vocabularies.Queries;

public record ExportVocabulariesQuery(string Format = "json") : IRequest<Result<ExportDataDto>>;

public record ExportDataDto(byte[] Bytes, string ContentType, string FileName);

public class ExportVocabulariesHandler : IRequestHandler<ExportVocabulariesQuery, Result<ExportDataDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFeatureGatingService _featureGating;

    public ExportVocabulariesHandler(IUnitOfWork uow, ICurrentUserService currentUser, IFeatureGatingService featureGating)
    {
        _uow = uow;
        _currentUser = currentUser;
        _featureGating = featureGating;
    }

    public async Task<Result<ExportDataDto>> Handle(ExportVocabulariesQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        
        var permissions = await _featureGating.GetPermissionsAsync(userId, ct);
        if (!permissions.CanExportData)
        {
            return Result<ExportDataDto>.Failure("ERR_PREMIUM_REQUIRED", 403);
        }

        var result = await _uow.Vocabularies.GetByUserIdAsync(userId, 1, int.MaxValue, null, null, null, ct);
        var vocabularies = result.Items;

        // Map to a cleaner format for export
        var exportList = vocabularies.Select(v => new
        {
            v.WordText,
            v.CustomMeaning,
            v.ContextSentence,
            AddedOn = v.CreatedAt.ToString("yyyy-MM-dd"),
            IsMastered = v.IsArchived
        });

        if (request.Format.ToLower() == "csv")
        {
            var csv = "Word,Meaning,Context,AddedOn,IsMastered\n" +
                      string.Join("\n", exportList.Select(x => 
                          $"\"{x.WordText}\",\"{x.CustomMeaning}\",\"{x.ContextSentence}\",\"{x.AddedOn}\",\"{x.IsMastered}\""));
                          
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return Result<ExportDataDto>.Success(new ExportDataDto(bytes, "text/csv", $"lexivocab_export_{DateTime.UtcNow:yyyyMMdd}.csv"));
        }
        else // default to json
        {
            var json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return Result<ExportDataDto>.Success(new ExportDataDto(bytes, "application/json", $"lexivocab_export_{DateTime.UtcNow:yyyyMMdd}.json"));
        }
    }
}
