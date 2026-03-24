using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using System.Text.Json;

namespace LexiVocab.Application.Features.Vocabularies.Queries;

public record ExportVocabulariesQuery(string Format = "json") : IRequest<Result<ExportDataDto>>, IFeatureGatedRequest
{
    public string FeatureCode => "EXPORT_ANKI";
    public string? QuotaLimitCode => null;
}

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
        try
        {
            var userId = _currentUser.UserId!.Value;
            
            var permissions = await _featureGating.GetPermissionsAsync(userId, ct);
            if (!permissions.HasFeature(request.FeatureCode))
            {
                return Result<ExportDataDto>.Forbidden("ERR_PREMIUM_REQUIRED", ErrorCode.AUTHZ_RESOURCE_FORBIDDEN);
            }

            var result = await _uow.Vocabularies.GetByUserIdAsync(userId, 1, int.MaxValue, null, null, null, ct);
            var vocabularies = result.Items;

            var exportList = vocabularies.Select(v => new
            {
                v.WordText,
                v.CustomMeaning,
                v.ContextSentence,
                AddedOn = v.CreatedAt.ToString("yyyy-MM-dd"),
                IsMastered = v.IsArchived,
                PhoneticUs = v.MasterVocabulary?.PhoneticUs,
                PhoneticUk = v.MasterVocabulary?.PhoneticUk,
                PartOfSpeech = v.MasterVocabulary?.PartOfSpeech,
                AudioUrl = v.MasterVocabulary?.AudioUrl,
                v.SourceUrl
            });

            var formatLower = request.Format.ToLowerInvariant();
            var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };

            if (formatLower == "csv")
            {
                var headers = "Word,Meaning,Context,AddedOn,IsMastered,PhoneticUs,PhoneticUk,PartOfSpeech,AudioUrl,SourceUrl";
                var csv = headers + "\n" +
                          string.Join("\n", exportList.Select(x => 
                          {
                              var word = (x.WordText ?? "").Replace("\"", "\"\"");
                              var meaning = (x.CustomMeaning ?? "").Replace("\"", "\"\"");
                              var context = (x.ContextSentence ?? "").Replace("\"", "\"\"");
                              var phoUs = (x.PhoneticUs ?? "").Replace("\"", "\"\"");
                              var phoUk = (x.PhoneticUk ?? "").Replace("\"", "\"\"");
                              var pos = (x.PartOfSpeech ?? "").Replace("\"", "\"\"");
                              var audio = (x.AudioUrl ?? "").Replace("\"", "\"\"");
                              var src = (x.SourceUrl ?? "").Replace("\"", "\"\"");
                              return $"\"{word}\",\"{meaning}\",\"{context}\",\"{x.AddedOn}\",\"{x.IsMastered}\",\"{phoUs}\",\"{phoUk}\",\"{pos}\",\"{audio}\",\"{src}\"";
                          }));
                              
                var bytes = utf8Bom.Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
                return Result<ExportDataDto>.Success(new ExportDataDto(bytes, "text/csv", $"lexivocab_export_{DateTime.UtcNow:yyyyMMdd}.csv"));
            }
            else if (formatLower == "quizlet")
            {
                var quizlet = string.Join("\n", exportList.Select(x => 
                {
                    var word = (x.WordText ?? "").Replace("\n", " ").Replace("\r", "").Replace("\t", " ");
                    var meaning = (x.CustomMeaning ?? "").Replace("\n", " ").Replace("\r", "").Replace("\t", " ");
                    return word + "\t" + meaning;
                }));
                var bytes = utf8Bom.Concat(System.Text.Encoding.UTF8.GetBytes(quizlet)).ToArray();
                return Result<ExportDataDto>.Success(new ExportDataDto(bytes, "text/plain", $"lexivocab_quizlet_{DateTime.UtcNow:yyyyMMdd}.txt"));
            }
            else if (formatLower == "txt")
            {
                var txt = string.Join("\n", exportList.Select((x, i) => 
                {
                    var word = (x.WordText ?? "").Replace("\n", " ").Replace("\r", "");
                    var meaning = (x.CustomMeaning ?? "").Replace("\n", " ").Replace("\r", "");
                    var meaningPart = string.IsNullOrEmpty(meaning) ? "" : " - " + meaning;
                    return (i + 1).ToString() + ". " + word + meaningPart;
                }));
                var bytes = utf8Bom.Concat(System.Text.Encoding.UTF8.GetBytes(txt)).ToArray();
                return Result<ExportDataDto>.Success(new ExportDataDto(bytes, "text/plain", $"lexivocab_export_{DateTime.UtcNow:yyyyMMdd}.txt"));
            }
            else // default to json
            {
                var json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
                var bytes = System.Text.Encoding.UTF8.GetBytes(json); // JSON doesn't strictly need BOM
                return Result<ExportDataDto>.Success(new ExportDataDto(bytes, "application/json", $"lexivocab_export_{DateTime.UtcNow:yyyyMMdd}.json"));
            }
        }
        catch (Exception)
        {
            return Result<ExportDataDto>.Failure("Failed to export vocabulary data.", 500, ErrorCode.VOCAB_EXPORT_FAILED);
        }
    }
}
