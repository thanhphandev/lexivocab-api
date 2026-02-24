using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LexiVocab.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI tools (migrations, scaffolding).
/// Used only by `dotnet ef` commands — bypasses the full DI container
/// so JWT/Redis/Hangfire config is not required.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Default connection for design-time (migrations)
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=lexivocab_dev;Username=postgres;Password=postgres",
            npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        return new AppDbContext(optionsBuilder.Options);
    }
}
