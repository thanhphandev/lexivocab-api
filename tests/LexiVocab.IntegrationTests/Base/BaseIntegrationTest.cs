using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace LexiVocab.IntegrationTests.Base;

/// <summary>
/// Base class for all integration tests requiring a database.
/// Spins up a Docker container with PostgreSQL 16 for each test class.
/// </summary>
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithDatabase("lexivocab_test")
        .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "trust")
        .Build();

    protected AppDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        DbContext = new AppDbContext(options);
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (DbContext != null)
        {
            await DbContext.DisposeAsync();
        }
        await _dbContainer.StopAsync();
    }
}
