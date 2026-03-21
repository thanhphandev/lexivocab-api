using System.Text.Json;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public abstract class BaseAIHandler
{
    protected (string? Provider, string? ModelId, string? BaseUrl, string? ApiKey, string? Model) ResolveCustomProvider(LexiVocab.Domain.Entities.User? user, string? originalProvider, string? originalModelId, string? originalBaseUrl, string? originalApiKey, string? originalModel)
    {
        string? resolvedProvider = originalProvider;
        string? resolvedModelId = originalModelId;
        string? resolvedBaseUrl = originalBaseUrl;
        string? resolvedApiKey = originalApiKey;
        string? resolvedModel = originalModel;

        if (user?.UserSetting != null && !string.IsNullOrWhiteSpace(user.UserSetting.CustomLlmsJson) && !string.IsNullOrWhiteSpace(originalProvider))
        {
            try {
                using var doc = JsonDocument.Parse(user.UserSetting.CustomLlmsJson);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("id", out var idProp) && idProp.GetString() == originalProvider)
                    {
                        resolvedProvider = "custom";
                        if (element.TryGetProperty("baseUrl", out var baseUrlProp)) resolvedBaseUrl = baseUrlProp.GetString();
                        if (element.TryGetProperty("apiKey", out var apiKeyProp)) resolvedApiKey = apiKeyProp.GetString();
                        if (element.TryGetProperty("model", out var modelProp)) {
                            resolvedModel = modelProp.GetString();
                            resolvedModelId = modelProp.GetString();
                        }
                        break;
                    }
                }
            } catch { }
        }
        return (resolvedProvider, resolvedModelId, resolvedBaseUrl, resolvedApiKey, resolvedModel);
    }

    protected async Task<Result<bool>> CheckTranslationQuotaAsync(IFeatureGatingService featureGating, Guid userId, LexiVocab.Domain.Entities.User? user, string? provider, CancellationToken ct)
    {
        string p = provider ?? "cloudflare";
        if (p == "custom" || p.StartsWith("google", StringComparison.OrdinalIgnoreCase) || p.StartsWith("bing", StringComparison.OrdinalIgnoreCase) || p.StartsWith("lingva", StringComparison.OrdinalIgnoreCase) || p.StartsWith("chrome-ai", StringComparison.OrdinalIgnoreCase))
        {
            return Result<bool>.Success(true);
        }

        bool isProModel = false;
        var permissions = await featureGating.GetPermissionsAsync(userId, ct);
        if (permissions.FeatureFlags.TryGetValue("AVAILABLE_LLM_MODELS", out var modelsJson))
        {
            try {
                using var doc = JsonDocument.Parse(modelsJson);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("id", out var idProp) && idProp.GetString() == p)
                    {
                        if (el.TryGetProperty("isPro", out var isProProp) && isProProp.ValueKind == JsonValueKind.True)
                            isProModel = true;
                        break;
                    }
                }
            } catch { }
        }

        if (isProModel && !await featureGating.HasFeatureAsync(userId, "ADVANCED_AI", ct))
        {
            return Result<bool>.Failure("This AI model requires an active Premium subscription.", 403);
        }

        if (!await featureGating.ConsumeQuotaAsync(userId, "AI_ACCESS", "LLM_TRANSLATION_LIMIT", ct))
        {
            return Result<bool>.Failure("Daily LLM translation limit reached. Upgrade to Pro for high volume translations.", 403);
        }

        return Result<bool>.Success(true);
    }
}
