using System.Collections.Concurrent;
using System.Reflection;
using LexiVocab.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Reads HTML email templates from embedded resources, caches them in-memory,
/// and replaces {{Placeholder}} tokens with provided values.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private static readonly ConcurrentDictionary<string, string> _templateCache = new();
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(ILogger<EmailTemplateService> logger)
    {
        _logger = logger;
    }

    public async Task<string> RenderTemplateAsync(string templateName, Dictionary<string, string> replacements)
    {
        var template = await GetTemplateAsync(templateName);

        foreach (var (key, value) in replacements)
        {
            template = template.Replace($"{{{{{key}}}}}", value);
        }

        return template;
    }

    private Task<string> GetTemplateAsync(string templateName)
    {
        if (_templateCache.TryGetValue(templateName, out var cached))
        {
            return Task.FromResult(cached);
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"LexiVocab.Infrastructure.EmailTemplates.{templateName}.html";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            _logger.LogError("Email template '{TemplateName}' not found as embedded resource: {ResourceName}", templateName, resourceName);
            throw new FileNotFoundException($"Email template '{templateName}' not found.", resourceName);
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        _templateCache.TryAdd(templateName, content);
        _logger.LogDebug("Email template '{TemplateName}' loaded and cached.", templateName);

        return Task.FromResult(content);
    }
}
