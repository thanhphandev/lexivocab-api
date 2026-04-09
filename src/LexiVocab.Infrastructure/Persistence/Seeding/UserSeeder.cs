using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the initial admin user if no admin exists in the system.
/// </summary>
public class UserSeeder : IDataSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;

    public int Order => 0; // Run first to ensure users exist

    public UserSeeder(AppDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        const string adminEmail = "admin@lexivocab.store";
        
        // Check if admin user already exists
        var existingAdmin = await _dbContext.Users
            .AnyAsync(u => u.Email == adminEmail || u.Role == UserRole.Admin, cancellationToken);

        if (!existingAdmin)
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                FullName = "System Administrator",
                PasswordHash = _passwordHasher.Hash("Thanh@041610"), // Default secure password
                Role = UserRole.Admin,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Users.AddAsync(adminUser, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
