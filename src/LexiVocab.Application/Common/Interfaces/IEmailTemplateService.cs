namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Renders HTML email templates with dynamic placeholder replacement.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Renders an HTML email template by name with placeholder replacements.
    /// Template placeholders use the {{Key}} syntax.
    /// </summary>
    /// <param name="templateName">Template file name without extension (e.g., "Welcome").</param>
    /// <param name="replacements">Dictionary of placeholder keys and their values.</param>
    /// <returns>Rendered HTML string.</returns>
    Task<string> RenderTemplateAsync(string templateName, Dictionary<string, string> replacements);
}
