using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

public class GlobalGameDeck : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string TargetLanguage { get; set; } = "en";
    
    /// <summary>JSON array of words in this deck. E.g., ["hello", "world", "apple"]</summary>
    public string WordsJson { get; set; } = "[]";
    
    public int DifficultyLevel { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
