using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexiVocab.Infrastructure.IntegrationTests.Base;

public abstract class BaseIntegrationTest : IDisposable
{
    protected AppDbContext DbContext { get; private set; }

    protected BaseIntegrationTest()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        DbContext.Database.EnsureDeleted();
        DbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
