using System.Collections.Generic;

namespace LexiVocab.Domain.Models.AI;

public class LlmRequest
{
    public List<LlmRequestMessage> Messages { get; set; } = new();
    public string? ModelId { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public bool Stream { get; set; } = false;
    public bool ResponseFormatJson { get; set; } = false;
    public string? ProviderName { get; set; }
}
