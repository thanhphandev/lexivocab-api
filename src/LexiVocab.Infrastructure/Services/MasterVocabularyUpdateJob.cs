using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<MasterVocabularyUpdateJob> _logger;

    public MasterVocabularyUpdateJob(
        IUnitOfWork uow,
        IDictionaryService dictService,
        ILogger<MasterVocabularyUpdateJob> logger)
    {
        _uow = uow;
        _dictService = dictService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("🚀 MasterVocabulary Enrichment Job starting...");

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

        foreach (var word in unlinkedWords)
        {
            // Check if Master record exists (it might have been created by another process recently)
            var master = await _uow.MasterVocabularies.GetByWordAsync(word.ToLower(), ct);

            if (master == null)
            {
                // Create skeleton or fetch from API immediately?
                // Let's fetch from API to avoid double work later
                master = await _dictService.FetchWordDefinitionAsync(word, ct);
                
                if (master == null)
                {
                    // If API fails or word not found, create a minimal skeleton to avoid repeated lookups
                    master = new MasterVocabulary { Word = word.ToLower(), IsFetchFailed = true };
                }

                await _uow.MasterVocabularies.AddAsync(master, ct);
                await _uow.SaveChangesAsync(ct); // Save to get ID
            }

            // Link all UserVocabularies with this word to this Master ID
            await _uow.Vocabularies.LinkToMasterAsync(word, master.Id, ct);
            
            // Delay to be nice to APIs if we called it
            await Task.Delay(500, ct); 
        }
    }

    private async Task ProcessPendingEnrichmentAsync(CancellationToken ct)
    {
        var pendingRecords = await _uow.MasterVocabularies.GetPendingEnrichmentAsync(limit: 50, ct);
        if (pendingRecords.Count == 0) return;

        _logger.LogInformation("Enriching {Count} existing master records...", pendingRecords.Count);

        int enrichedCount = 0;
        foreach (var vocab in pendingRecords)
        {
            var info = await _dictService.FetchWordDefinitionAsync(vocab.Word, ct);
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
