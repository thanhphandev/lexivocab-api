using LexiVocab.Application.Common.Interfaces;

namespace LexiVocab.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
