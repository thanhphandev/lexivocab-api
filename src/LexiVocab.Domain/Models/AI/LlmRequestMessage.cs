namespace LexiVocab.Domain.Models.AI;

public class LlmRequestMessage
{
    public string Role { get; set; } = string.Empty; // "system", "user", "assistant"
    public string Content { get; set; } = string.Empty;
}
