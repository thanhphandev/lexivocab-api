using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Review;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Reviews.Commands;

/// <summary>
/// Submit a review result for a flashcard. 
/// Triggers SM-2 recalculation and creates an immutable audit log entry.
/// </summary>
public record SubmitReviewCommand(
    Guid UserVocabularyId,
    QualityScore QualityScore,
    int? TimeSpentMs
) : IRequest<Result<ReviewResultDto>>;

public class SubmitReviewHandler : IRequestHandler<SubmitReviewCommand, Result<ReviewResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISrsAlgorithm _srs;

    public SubmitReviewHandler(IUnitOfWork uow, ICurrentUserService currentUser, ISrsAlgorithm srs)
    {
        _uow = uow;
        _currentUser = currentUser;
        _srs = srs;
    }

    public async Task<Result<ReviewResultDto>> Handle(SubmitReviewCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var vocab = await _uow.Vocabularies.GetByIdAsync(request.UserVocabularyId, ct);

        if (vocab is null || vocab.UserId != userId)
            return Result<ReviewResultDto>.NotFound("Vocabulary card not found.");

        // ─── SM-2 Calculation ─────────────────────────────────
        var result = _srs.Calculate(
            vocab.RepetitionCount,
            vocab.EasinessFactor,
            vocab.IntervalDays,
            request.QualityScore);

        // ─── Update Vocabulary SRS State ──────────────────────
        vocab.RepetitionCount = result.NewRepetitionCount;
        vocab.EasinessFactor = result.NewEasinessFactor;
        vocab.IntervalDays = result.NewIntervalDays;
        vocab.NextReviewDate = result.NextReviewDate;
        vocab.LastReviewedAt = DateTime.UtcNow;
        vocab.UpdatedAt = DateTime.UtcNow;

        _uow.Vocabularies.Update(vocab);

        // ─── Create Immutable Review Log ──────────────────────
        var log = new ReviewLog
        {
            UserVocabularyId = request.UserVocabularyId,
            UserId = userId, // Denormalized for fast analytics
            QualityScore = request.QualityScore,
            TimeSpentMs = request.TimeSpentMs,
            ReviewedAt = DateTime.UtcNow
        };

        await _uow.ReviewLogs.AddAsync(log, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ReviewResultDto>.Success(new ReviewResultDto(
            vocab.Id,
            result.NewRepetitionCount,
            result.NewEasinessFactor,
            result.NewIntervalDays,
            result.NextReviewDate));
    }
}
