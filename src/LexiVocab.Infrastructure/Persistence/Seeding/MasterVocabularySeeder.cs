using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Persistence.Seeding;

public class MasterVocabularySeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<MasterVocabularySeeder> _logger;

    public MasterVocabularySeeder(AppDbContext context, ILogger<MasterVocabularySeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 10; // Run after base definitions if needed

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.MasterVocabularies.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("MasterVocabulary already has data. Skipping...");
            return;
        }

        var words = new List<MasterVocabulary>
        {
            // --- A1-A2 (Beginner) ---
            new() { Word = "frequent", PartOfSpeech = "adjective", Meaning = "Happening or occurring many times at short intervals.", CefrLevel = "A2", PopularityRank = 1200 },
            new() { Word = "versatile", PartOfSpeech = "adjective", Meaning = "Able to adapt or be adapted to many different functions or activities.", CefrLevel = "B2", PopularityRank = 2500 },
            new() { Word = "commute", PartOfSpeech = "verb", Meaning = "Travel some distance between one's home and place of work on a regular basis.", CefrLevel = "B1", PopularityRank = 1800 },
            new() { Word = "habit", PartOfSpeech = "noun", Meaning = "A settled or regular tendency or practice, especially one that is hard to give up.", CefrLevel = "A2", PopularityRank = 1100 },
            new() { Word = "explore", PartOfSpeech = "verb", Meaning = "Travel in or through (an unfamiliar country or area) in order to learn about or familiarize oneself with it.", CefrLevel = "A2", PopularityRank = 1300 },
            
            // --- B1-B2 (Intermediate) ---
            new() { Word = "resilient", PartOfSpeech = "adjective", Meaning = "Able to withstand or recover quickly from difficult conditions.", CefrLevel = "B2", PopularityRank = 3000 },
            new() { Word = "innovative", PartOfSpeech = "adjective", Meaning = "Featuring new methods; advanced and original.", CefrLevel = "B2", PopularityRank = 1500 },
            new() { Word = "leverage", PartOfSpeech = "verb", Meaning = "Use (something) to maximum advantage.", CefrLevel = "B2", PopularityRank = 2200 },
            new() { Word = "volatile", PartOfSpeech = "adjective", Meaning = "Liable to change rapidly and unpredictably, especially for the worse.", CefrLevel = "B2", PopularityRank = 3500 },
            new() { Word = "elaborate", PartOfSpeech = "adjective", Meaning = "Involving many carefully arranged parts or details; detailed and complicated in design and planning.", CefrLevel = "B2", PopularityRank = 2800 },
            new() { Word = "sustainable", PartOfSpeech = "adjective", Meaning = "Able to be maintained at a certain rate or level.", CefrLevel = "B2", PopularityRank = 2100 },
            new() { Word = "inequality", PartOfSpeech = "noun", Meaning = "Difference in size, degree, circumstances, etc.; lack of equality.", CefrLevel = "B2", PopularityRank = 3200 },
            new() { Word = "authentic", PartOfSpeech = "adjective", Meaning = "Of undisputed origin; genuine.", CefrLevel = "B2", PopularityRank = 2600 },
            new() { Word = "advocacy", PartOfSpeech = "noun", Meaning = "Public support for or recommendation of a particular cause or policy.", CefrLevel = "B2", PopularityRank = 4100 },
            
            // --- C1-C2 (Advanced & Academic) ---
            new() { Word = "ubiquitous", PartOfSpeech = "adjective", Meaning = "Present, appearing, or found everywhere.", CefrLevel = "C1", PopularityRank = 4500 },
            new() { Word = "ephemeral", PartOfSpeech = "adjective", Meaning = "Lasting for a very short time.", CefrLevel = "C2", PopularityRank = 7000 },
            new() { Word = "melancholy", PartOfSpeech = "noun", Meaning = "A feeling of pensive sadness, typically with no obvious cause.", CefrLevel = "C1", PopularityRank = 5000 },
            new() { Word = "euphoria", PartOfSpeech = "noun", Meaning = "A feeling or state of intense excitement and happiness.", CefrLevel = "C2", PopularityRank = 6000 },
            new() { Word = "altruistic", PartOfSpeech = "adjective", Meaning = "Showing a disinterested and selfless concern for the well-being of others; unselfish.", CefrLevel = "C1", PopularityRank = 5500 },
            new() { Word = "petrichor", PartOfSpeech = "noun", Meaning = "A pleasant smell that frequently accompanies the first rain after a long period of warm, dry weather.", CefrLevel = "C2", PopularityRank = 15000 },
            new() { Word = "synergy", PartOfSpeech = "noun", Meaning = "The interaction or cooperation of two or more organizations, substances, or other agents to produce a combined effect greater than the sum of their separate effects.", CefrLevel = "C1", PopularityRank = 4000 },
            new() { Word = "pedagogy", PartOfSpeech = "noun", Meaning = "The method and practice of teaching, especially as an academic subject or theoretical concept.", CefrLevel = "C2", PopularityRank = 8200 },
            new() { Word = "hypothesis", PartOfSpeech = "noun", Meaning = "A supposition or proposed explanation made on the basis of limited evidence as a starting point for further investigation.", CefrLevel = "C1", PopularityRank = 3400 },
            new() { Word = "empirical", PartOfSpeech = "adjective", Meaning = "Based on, concerned with, or verifiable by observation or experience rather than theory or pure logic.", CefrLevel = "C1", PopularityRank = 4800 },
            new() { Word = "paradigm", PartOfSpeech = "noun", Meaning = "A typical example or pattern of something; a model.", CefrLevel = "C1", PopularityRank = 4600 },
            new() { Word = "poignant", PartOfSpeech = "adjective", Meaning = "Evoking a keen sense of sadness or regret.", CefrLevel = "C1", PopularityRank = 5900 },
            new() { Word = "eclectic", PartOfSpeech = "adjective", Meaning = "Deriving ideas, style, or taste from a broad and diverse range of sources.", CefrLevel = "C1", PopularityRank = 6100 },
            new() { Word = "cognitive", PartOfSpeech = "adjective", Meaning = "Relating to the mental action or process of acquiring knowledge and understanding through thought, experience, and the senses.", CefrLevel = "C1", PopularityRank = 2900 },
            new() { Word = "incentive", PartOfSpeech = "noun", Meaning = "A thing that motivates or encourages one to do something.", CefrLevel = "B2", PopularityRank = 1700 },
            
            // --- Advanced Action Verbs ---
            new() { Word = "precipitate", PartOfSpeech = "verb", Meaning = "Cause (an event or situation, typically one that is bad or undesirable) to happen suddenly, unexpectedly, or prematurely.", CefrLevel = "C2", PopularityRank = 7500 },
            new() { Word = "circumvent", PartOfSpeech = "verb", Meaning = "Find a way around (an obstacle).", CefrLevel = "C1", PopularityRank = 6300 },
            new() { Word = "undermine", PartOfSpeech = "verb", Meaning = "Lessen the effectiveness, power, or ability of, especially gradually or insidiously.", CefrLevel = "C1", PopularityRank = 3700 },
            new() { Word = "consolidate", PartOfSpeech = "verb", Meaning = "Make (something physically stronger or more solid).", CefrLevel = "B2", PopularityRank = 2400 },
            new() { Word = "scrutinize", PartOfSpeech = "verb", Meaning = "Examine or inspect closely and thoroughly.", CefrLevel = "C1", PopularityRank = 4700 },
            
            // --- Tech & Infrastructure ---
            new() { Word = "algorithm", PartOfSpeech = "noun", Meaning = "A process or set of rules to be followed in calculations or other problem-solving operations, especially by a computer.", CefrLevel = "B2", PopularityRank = 800 },
            new() { Word = "infrastructure", PartOfSpeech = "noun", Meaning = "The basic physical and organizational structures and facilities needed for the operation of a society or enterprise.", CefrLevel = "B2", PopularityRank = 600 },
            new() { Word = "protocol", PartOfSpeech = "noun", Meaning = "The official procedure or system of rules governing affairs of state or diplomatic occasions.", CefrLevel = "B2", PopularityRank = 1200 },
            new() { Word = "aggregate", PartOfSpeech = "verb", Meaning = "Form or group into a class or cluster.", CefrLevel = "C1", PopularityRank = 3200 },
            new() { Word = "optimize", PartOfSpeech = "verb", Meaning = "Make the best or most effective use of (a situation, opportunity, or resource).", CefrLevel = "B2", PopularityRank = 1400 },
            
            // --- Diverse / Miscellaneous ---
            new() { Word = "aesthetic", PartOfSpeech = "adjective", Meaning = "Concerned with beauty or the appreciation of beauty.", CefrLevel = "B2", PopularityRank = 2000 },
            new() { Word = "diligent", PartOfSpeech = "adjective", Meaning = "Having or showing care and conscientiousness in one's work or duties.", CefrLevel = "B1", PopularityRank = 3800 },
            new() { Word = "eloquent", PartOfSpeech = "adjective", Meaning = "Fluent or persuasive in speaking or writing.", CefrLevel = "C1", PopularityRank = 4200 },
            new() { Word = "frugal", PartOfSpeech = "adjective", Meaning = "Sparing or economical with regard to money or food.", CefrLevel = "B2", PopularityRank = 5200 },
            new() { Word = "gregarious", PartOfSpeech = "adjective", Meaning = "(of a person) fond of company; sociable.", CefrLevel = "C1", PopularityRank = 6500 },
            new() { Word = "hinder", PartOfSpeech = "verb", Meaning = "Create difficulties for (someone or something), resulting in delay or obstruction.", CefrLevel = "B2", PopularityRank = 2400 },
            new() { Word = "impeccable", PartOfSpeech = "adjective", Meaning = "(of behavior, performance, or appearance) in accordance with the highest standards; faultless.", CefrLevel = "C1", PopularityRank = 7200 },
            new() { Word = "jovial", PartOfSpeech = "adjective", Meaning = "Cheerful and friendly.", CefrLevel = "B2", PopularityRank = 8500 },
            new() { Word = "kinship", PartOfSpeech = "noun", Meaning = "Blood relationship.", CefrLevel = "B2", PopularityRank = 9200 },
            new() { Word = "lucid", PartOfSpeech = "adjective", Meaning = "Expressed clearly; easy to understand.", CefrLevel = "C1", PopularityRank = 4800 },
            new() { Word = "mitigate", PartOfSpeech = "verb", Meaning = "Make less severe, serious, or painful.", CefrLevel = "C1", PopularityRank = 3100 },
            new() { Word = "nuance", PartOfSpeech = "noun", Meaning = "A subtle difference in or shade of meaning, expression, or sound.", CefrLevel = "C2", PopularityRank = 5600 },
            new() { Word = "obsolete", PartOfSpeech = "adjective", Meaning = "No longer produced or used; out of date.", CefrLevel = "B2", PopularityRank = 4300 },
            new() { Word = "pervasive", PartOfSpeech = "adjective", Meaning = "(especially of an unwelcome influence or physical effect) spreading widely throughout an area or a group of people.", CefrLevel = "C1", PopularityRank = 5400 },
            new() { Word = "quaint", PartOfSpeech = "adjective", Meaning = "Attractively unusual or old-fashioned.", CefrLevel = "B2", PopularityRank = 6800 },
            new() { Word = "relevance", PartOfSpeech = "noun", Meaning = "The quality or state of being closely connected or appropriate.", CefrLevel = "B2", PopularityRank = 1100 },
            new() { Word = "tenacious", PartOfSpeech = "adjective", Meaning = "Tending to keep a firm hold of something; clinging or adhering closely.", CefrLevel = "C1", PopularityRank = 5800 },
            new() { Word = "vibrant", PartOfSpeech = "adjective", Meaning = "Full of energy and enthusiasm.", CefrLevel = "B2", PopularityRank = 1900 },
            new() { Word = "wary", PartOfSpeech = "adjective", Meaning = "Feeling or showing caution about possible dangers or problems.", CefrLevel = "B2", PopularityRank = 3600 },
            new() { Word = "zeal", PartOfSpeech = "noun", Meaning = "Great energy or enthusiasm in pursuit of a cause or an objective.", CefrLevel = "C1", PopularityRank = 6100 },
            new() { Word = "abstain", PartOfSpeech = "verb", Meaning = "Restrain oneself from doing or enjoying something.", CefrLevel = "B2", PopularityRank = 7300 },
            new() { Word = "belligerent", PartOfSpeech = "adjective", Meaning = "Hostile and aggressive.", CefrLevel = "C1", PopularityRank = 8400 },
            new() { Word = "capitulate", PartOfSpeech = "verb", Meaning = "Cease to resist an opponent or an unwelcome demand; surrender.", CefrLevel = "C2", PopularityRank = 9500 },
            new() { Word = "defer", PartOfSpeech = "verb", Meaning = "Put off (an action or event) to a later time; postpone.", CefrLevel = "B2", PopularityRank = 3900 },
            new() { Word = "enigma", PartOfSpeech = "noun", Meaning = "A person or thing that is mysterious, puzzling, or difficult to understand.", CefrLevel = "C1", PopularityRank = 5700 },
            new() { Word = "fastidious", PartOfSpeech = "adjective", Meaning = "Very attentive to and concerned about accuracy and detail.", CefrLevel = "C1", PopularityRank = 6900 },
            new() { Word = "gullible", PartOfSpeech = "adjective", Meaning = "Easily persuaded to believe something; credulous.", CefrLevel = "B2", PopularityRank = 8100 },
            new() { Word = "haughty", PartOfSpeech = "adjective", Meaning = "Arrogantly superior and disdainful.", CefrLevel = "C1", PopularityRank = 7800 },
            new() { Word = "impetuous", PartOfSpeech = "adjective", Meaning = "Acting or done quickly and without thought or care.", CefrLevel = "C1", PopularityRank = 7600 },
            new() { Word = "laconic", PartOfSpeech = "adjective", Meaning = "(of a person, speech, or style of writing) using very few words.", CefrLevel = "C2", PopularityRank = 9100 },
            new() { Word = "mundane", PartOfSpeech = "adjective", Meaning = "Lacking interest or excitement; dull.", CefrLevel = "B2", PopularityRank = 4400 },
            new() { Word = "nostalgia", PartOfSpeech = "noun", Meaning = "A sentimental longing or wistful affection for the period in the past.", CefrLevel = "B2", PopularityRank = 2300 },
            new() { Word = "opulent", PartOfSpeech = "adjective", Meaning = "Ostentatiously rich and luxurious or lavish.", CefrLevel = "C1", PopularityRank = 8700 },
            new() { Word = "pragmatic", PartOfSpeech = "adjective", Meaning = "Dealing with things sensibly and realistically in a way that is based on practical rather than theoretical considerations.", CefrLevel = "C1", PopularityRank = 3300 },
            new() { Word = "quintessential", PartOfSpeech = "adjective", Meaning = "Representing the most perfect or typical example of a quality or class.", CefrLevel = "C2", PopularityRank = 8900 },
            new() { Word = "recapitulate", PartOfSpeech = "verb", Meaning = "Summarize and state again the main points of.", CefrLevel = "C2", PopularityRank = 9800 },
            new() { Word = "sagacious", PartOfSpeech = "adjective", Meaning = "Having or showing keen mental discernment and good judgment; shrewd.", CefrLevel = "C2", PopularityRank = 10500 },
            new() { Word = "taciturn", PartOfSpeech = "adjective", Meaning = "(of a person) reserved or uncommunicative in speech; saying little.", CefrLevel = "C2", PopularityRank = 11200 },
            new() { Word = "unfathomable", PartOfSpeech = "adjective", Meaning = "Incapable of being fully explored or understood.", CefrLevel = "C1", PopularityRank = 6600 },
            new() { Word = "venerable", PartOfSpeech = "adjective", Meaning = "Accorded a great deal of respect, especially because of age, wisdom, or character.", CefrLevel = "C1", PopularityRank = 7400 },
            new() { Word = "wistful", PartOfSpeech = "adjective", Meaning = "Having or showing a feeling of vague or regretful longing.", CefrLevel = "B2", PopularityRank = 5300 }
        };

        await _context.MasterVocabularies.AddRangeAsync(words, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully seeded {Count} master vocabulary words.", words.Count);
    }
}
