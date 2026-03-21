using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Games;
using LexiVocab.Domain.Interfaces;
using MediatR;
using LexiVocab.Domain.Entities;
using System.Text.Json;

namespace LexiVocab.Application.Features.Games;

// ─── Get Hub Data ───────────────────────────────────────────────
public record GetGameHubDataQuery : IRequest<Result<GameHubDataDto>>;

public class GetGameHubDataHandler : IRequestHandler<GetGameHubDataQuery, Result<GameHubDataDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetGameHubDataHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<GameHubDataDto>> Handle(GetGameHubDataQuery request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(_currentUser.UserId!.Value, ct);
        if (user is null) return Result<GameHubDataDto>.NotFound();

        var stats = user.UserGameStatistic ?? new UserGameStatistic { Hearts = 3, TotalXP = 0, CurrentLevel = 1 };

        // For now, return empty global decks. We will link GlobalGameDeck Repo shortly.
        var globalDecks = new List<GlobalDeckDto>
        {
            new GlobalDeckDto(Guid.NewGuid(), "Starter Deck: Top 50 English Words", "Perfect for beginners. Start learning English now!", null, 1, "English")
        };

        return Result<GameHubDataDto>.Success(new GameHubDataDto(
            stats.TotalXP,
            stats.CurrentLevel,
            stats.CurrentStreak,
            stats.Hearts,
            globalDecks
        ));
    }
}

// ─── Get Session Data ─────────────────────────────────────────────
public record GetGameSessionDataQuery(string Mode, Guid? DeckId, int Count = 3) : IRequest<Result<GameSessionResponseDto>>;

public class GetGameSessionDataHandler : IRequestHandler<GetGameSessionDataQuery, Result<GameSessionResponseDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAIService _aiService;

    public GetGameSessionDataHandler(IUnitOfWork uow, ICurrentUserService currentUser, IAIService aiService)
    {
        _uow = uow;
        _currentUser = currentUser;
        _aiService = aiService;
    }

    public async Task<Result<GameSessionResponseDto>> Handle(GetGameSessionDataQuery request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(_currentUser.UserId!.Value, ct);
        if (user is null) return Result<GameSessionResponseDto>.NotFound();

        string tl = user.UserSetting?.TargetLanguage ?? "English";
        string ul = user.UserSetting?.NativeLanguage ?? "Vietnamese";

        // Fallback words if user has no vocab
        var baseWords = new[] { "apple", "serendipity", "coffee", "breeze", "enlighten" }.OrderBy(x => Guid.NewGuid()).Take(request.Count).ToList();
        
        var questions = new List<GameQuestionDto>();
        var types = new[] { "MultipleChoice", "FillBlank", "Pronunciation" };

        for (int i = 0; i < request.Count; i++)
        {
            var word = baseWords[i % baseWords.Count];
            var type = types[i % types.Length];

            if (type == "MultipleChoice")
            {
                var json = await _aiService.GenerateQuizAsync(word, tl, ul, ct);
                if (!string.IsNullOrEmpty(json))
                {
                    try {
                        var quiz = JsonSerializer.Deserialize<LexiVocab.Application.DTOs.AI.QuizDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (quiz != null) {
                            questions.Add(new GameQuestionDto("MultipleChoice", word, null, quiz.Options, quiz.CorrectIndex, quiz.Explanation));
                            continue;
                        }
                    } catch {}
                }
            }
            else if (type == "FillBlank")
            {
                var json = await _aiService.GenerateFillBlankAsync(word, tl, ul, ct);
                if (!string.IsNullOrEmpty(json))
                {
                    try {
                        // Minimal parsing for the FillBlank JSON shape: { sentence, translation, options, explanation }
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var sentence = root.GetProperty("sentence").GetString();
                        var correctWord = root.GetProperty("correctWord").GetString();
                        var explanation = root.GetProperty("explanation").GetString();
                        var opts = root.GetProperty("options").EnumerateArray().Select(x => x.GetString()!).ToList();
                        int correctIdx = opts.IndexOf(correctWord!);
                        if (correctIdx == -1) { correctIdx = 0; opts[0] = correctWord!; }

                        questions.Add(new GameQuestionDto("FillBlank", word, sentence, opts, correctIdx, explanation));
                        continue;
                    } catch {}
                }
            }

            // Fallback or Pronunciation
            questions.Add(new GameQuestionDto("Pronunciation", word, "Speak this word clearly.", new List<string>(), -1, null));
        }

        return Result<GameSessionResponseDto>.Success(new GameSessionResponseDto(Guid.NewGuid(), questions));
    }
}
