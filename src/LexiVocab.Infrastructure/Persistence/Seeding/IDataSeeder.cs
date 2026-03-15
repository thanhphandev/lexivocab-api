namespace LexiVocab.Infrastructure.Persistence.Seeding;

/// <summary>
/// Interface for data seeders that populate initial data into the database.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Sequencing order for seeders. Lower values run first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Seeds data into the database if none exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
