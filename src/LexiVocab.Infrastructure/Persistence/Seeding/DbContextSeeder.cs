using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Persistence.Seeding;

/// <summary>
/// Orchestrates database seeding by running all registered IDataSeeder implementations in order.
/// </summary>
public class DbContextSeeder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DbContextSeeder> _logger;

    public DbContextSeeder(IServiceProvider serviceProvider, ILogger<DbContextSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Seeds all data by running registered seeders in order.
    /// </summary>
    public async Task SeedAllAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Ensure database is created
        await dbContext.Database.MigrateAsync(cancellationToken);
        
        // Get all seeders and run them in order
        var seeders = scope.ServiceProvider.GetServices<IDataSeeder>()
            .OrderBy(s => s.Order)
            .ToList();

        foreach (var seeder in seeders)
        {
            var seederName = seeder.GetType().Name;
            try
            {
                _logger.LogInformation("Running seeder: {SeederName} (Order: {Order})", seederName, seeder.Order);
                await seeder.SeedAsync(cancellationToken);
                _logger.LogInformation("Seeder {SeederName} completed successfully", seederName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running seeder {SeederName}", seederName);
                throw;
            }
        }
    }
}
