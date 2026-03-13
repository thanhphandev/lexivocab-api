using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Hangfire job that identifies incomplete MasterVocabulary records 
/// and enriches them via external dictionary APIs.
/// Also links orphaned UserVocabularies to MasterVocabulary records.
/// </summary>
public class MasterVocabularyUpdateJob : IMasterVocabularyUpdateJob
{
    private readonly IUnitOfWork _uow;
    private readonly IDictionaryService _dictService;
    private readonly IAIService _aiService;
    private readonly ILogger<MasterVocabularyUpdateJob> _logger;
    private readonly IConfiguration _configuration;

    public MasterVocabularyUpdateJob(
        IUnitOfWork uow,
        IDictionaryService dictService,
        IAIService aiService,
        ILogger<MasterVocabularyUpdateJob> logger,
        IConfiguration configuration)
    {
        _uow = uow;
        _dictService = dictService;
        _aiService = aiService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("🚀 MasterVocabulary Enrichment Job starting (AI-enhanced)...");

        try
        {
            // Phase 1: Link and Create
            await ProcessUnlinkedWordsAsync(ct);

            // Phase 2: Enrich Existing
            await ProcessPendingEnrichmentAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Critical error in MasterVocabularyUpdateJob");
            throw;
        }

        _logger.LogInformation("✅ MasterVocabulary Enrichment Job finished.");
    }

    private async Task ProcessUnlinkedWordsAsync(CancellationToken ct)
    {
        var unlinkedWords = await _uow.Vocabularies.GetUnlinkedWordsAsync(limit: 50, ct);
        if (unlinkedWords.Count == 0) return;

        _logger.LogInformation("Found {Count} unlinked words. Linking...", unlinkedWords.Count);

        int aiEnrichedThisRun = 0;
        var preferredSource = _configuration["CloudflareAI:PreferredSource"] ?? "AI";
        var maxAiEnrichments = _configuration.GetValue<int>("CloudflareAI:MaxAiEnrichmentsPerRun", 10);

        foreach (var word in unlinkedWords)
        {
            var master = await _uow.MasterVocabularies.GetByWordAsync(word.ToLower(), ct);

            if (master == null)
            {
                if (preferredSource == "AI" && aiEnrichedThisRun < maxAiEnrichments)
                {
                    master = await _aiService.EnrichWordAsync(word, ct);
                    if (master != null) aiEnrichedThisRun++;
                }
                else if (preferredSource == "API")
                {
                    master = await _dictService.FetchWordDefinitionAsync(word, ct);
                }
                
                // Fallback if preferred failed
                if (master == null)
                {
                    if (preferredSource == "AI")
                    {
                        _logger.LogInformation("AI skipped or failed for '{Word}', falling back to dictionary service...", word);
                        master = await _dictService.FetchWordDefinitionAsync(word, ct);
                    }
                    else if (preferredSource == "API" && aiEnrichedThisRun < maxAiEnrichments)
                    {
                        _logger.LogInformation("API failed for '{Word}', falling back to AI enrichment...", word);
                        master = await _aiService.EnrichWordAsync(word, ct);
                        if (master != null) aiEnrichedThisRun++;
                    }
                }
                
                if (master == null)
                {
                    master = new MasterVocabulary { Word = word.ToLower(), IsFetchFailed = true };
                }

                await _uow.MasterVocabularies.AddAsync(master, ct);
                await _uow.SaveChangesAsync(ct);
            }

            await _uow.Vocabularies.LinkToMasterAsync(word, master.Id, ct);
            await Task.Delay(500, ct); 
        }
    }

    private async Task ProcessPendingEnrichmentAsync(CancellationToken ct)
    {
        var pendingRecords = await _uow.MasterVocabularies.GetPendingEnrichmentAsync(limit: 50, ct);
        if (pendingRecords.Count == 0) return;

        _logger.LogInformation("Enriching {Count} existing master records...", pendingRecords.Count);

        var preferredSource = _configuration["CloudflareAI:PreferredSource"] ?? "AI";
        int enrichedCount = 0;

        foreach (var vocab in pendingRecords)
        {
            MasterVocabulary? info = null;

            if (preferredSource == "AI")
            {
                info = await _aiService.EnrichWordAsync(vocab.Word, ct) 
                      ?? await _dictService.FetchWordDefinitionAsync(vocab.Word, ct);
            }
            else
            {
                info = await _dictService.FetchWordDefinitionAsync(vocab.Word, ct)
                      ?? await _aiService.EnrichWordAsync(vocab.Word, ct);
            }

            if (info != null)
            {
                vocab.PartOfSpeech ??= info.PartOfSpeech;
                vocab.PhoneticUk ??= info.PhoneticUk;
                vocab.PhoneticUs ??= info.PhoneticUs;
                vocab.AudioUrl ??= info.AudioUrl;
                enrichedCount++;
            }
            else
            {
                vocab.IsFetchFailed = true;
            }
            
            await Task.Delay(500, ct);
        }

        if (pendingRecords.Count > 0)
        {
            await _uow.SaveChangesAsync(ct);
            _logger.LogInformation("Processed {Count} records (Enriched: {EnrichedCount}).", pendingRecords.Count, enrichedCount);
        }
    }
}
