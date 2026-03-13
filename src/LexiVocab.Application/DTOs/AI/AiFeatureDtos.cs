namespace LexiVocab.Application.DTOs.AI;

public record WordExplanationDto(
    string Explanation,
    List<string> Nuances,
    List<string> Tips
);

public record RelatedWordsDto(
    List<string> Synonyms,
    List<string> Antonyms,
    List<string> Collocations
);

public record QuizDto(
    string Question,
    List<string> Options,
    int CorrectIndex,
    string Explanation
);
