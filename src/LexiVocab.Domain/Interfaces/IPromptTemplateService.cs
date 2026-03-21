using System.Collections.Generic;
using System.Threading.Tasks;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Models.AI;

namespace LexiVocab.Domain.Interfaces;

public interface IPromptTemplateService
{
    Task<LlmRequest> BuildRequestAsync(
        AIUseCase useCase,
        Dictionary<string, string> parameters,
        string? overriddenModelId = null,
        bool responseFormatJson = false);
}
