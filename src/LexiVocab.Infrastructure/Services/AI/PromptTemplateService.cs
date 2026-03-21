using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Models.AI;

namespace LexiVocab.Infrastructure.Services.AI;

public class PromptTemplateService : IPromptTemplateService
{
    private static readonly Dictionary<AIUseCase, string> UseCaseFileMap = new()
    {
        { AIUseCase.Translation, "Translation.txt" },
        { AIUseCase.ExplainUsage, "ExplainUsage.txt" },
        { AIUseCase.GenerateStory, "GenerateStory.txt" },
        { AIUseCase.SuggestRelated, "SuggestRelated.txt" },
        { AIUseCase.GenerateQuiz, "GenerateQuiz.txt" },
        { AIUseCase.InputTranslation, "InputTranslation.txt" }
    };

    public async Task<LlmRequest> BuildRequestAsync(
        AIUseCase useCase,
        Dictionary<string, string> parameters,
        string? overriddenModelId = null,
        bool responseFormatJson = true)
    {
        if (!UseCaseFileMap.TryGetValue(useCase, out var fileName))
        {
            throw new NotSupportedException($"UseCase {useCase} has no prompt file mapped.");
        }

        var promptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Services", "AI", "Prompts");
        var filePath = Path.Combine(promptsDir, fileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Prompt template not found at {filePath}");
        }

        var templateContent = await File.ReadAllTextAsync(filePath);

        foreach (var kvp in parameters)
        {
            templateContent = templateContent.Replace("{{" + kvp.Key + "}}", kvp.Value);
        }

        var request = new LlmRequest
        {
            ModelId = overriddenModelId,
            Stream = false, // Controlled by orchestrator/provider logic
            ResponseFormatJson = responseFormatJson,
            Messages = new List<LlmRequestMessage>
            {
                new LlmRequestMessage { Role = "user", Content = templateContent }
            }
        };

        return request;
    }
}
