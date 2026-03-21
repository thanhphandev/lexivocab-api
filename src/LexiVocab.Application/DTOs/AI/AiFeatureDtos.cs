namespace LexiVocab.Application.DTOs.AI;

/// <summary>Represents a detailed explanation of a word's usage and nuances.</summary>
public record WordExplanationDto(
    /// <summary>A plain-text explanation of the word's primary meaning and usage.</summary>
    string Explanation,
    /// <summary>A list of subtle differences in meaning or formal/informal usage patterns.</summary>
    List<string> Nuances,
    /// <summary>Practical examples of the word being used in sentences.</summary>
    List<string> Examples,
    /// <summary>A professional mnemonic or 'Pro Tip' for learners.</summary>
    string Tip
);

/// <summary>Represents words and phrases related to a target word.</summary>
public record RelatedWordsDto(
    /// <summary>Words with similar meanings.</summary>
    List<string> Synonyms,
    /// <summary>Words with opposite meanings.</summary>
    List<string> Antonyms,
    /// <summary>Words that frequently appear together with the target word.</summary>
    List<string> Collocations,
    /// <summary>A creative, easy-to-remember mnemonic trick.</summary>
    string? Mnemonic
);

/// <summary>Represents a generated multiple-choice quiz question.</summary>
public record QuizDto(
    /// <summary>The quiz question text.</summary>
    string Question,
    /// <summary>A list of 4 possible answers.</summary>
    List<string> Options,
    /// <summary>The zero-based index of the correct answer in the Options array.</summary>
    int CorrectIndex,
    /// <summary>A detailed explanation of why the correct answer is right.</summary>
    string Explanation
);

/// <summary>Represents a request for streaming real-time input translation.</summary>
public record StreamInputTranslateRequest(
    string Text,
    string? TargetLanguage,
    string? Style,
    string? Provider,
    string? ModelId,
    string? CustomBaseUrl,
    string? CustomApiKey,
    string? CustomModel
);
