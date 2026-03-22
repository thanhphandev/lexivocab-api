using System.Collections.Generic;

namespace LexiVocab.Domain.Models;

/// <summary>
/// Additional contextual information for error responses.
/// Used for parameterized error messages and client-side logic.
/// </summary>
public class ErrorDetails
{
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<ValidationError>? ValidationErrors { get; set; }
    public int? RetryAfterSeconds { get; set; }
    public string? ReferenceId { get; set; }
    
    public void AddParameter(string key, object value)
    {
        Parameters[key] = value;
    }
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
