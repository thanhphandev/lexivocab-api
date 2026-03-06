using Hangfire;
using Hangfire.Common;
using Hangfire.InMemory;
using Hangfire.States;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LexiVocab.API.IntegrationTests.Base;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the app's AppDbContext registration and all EF Core options.
            var dbContextDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType.Name.Contains("DbContextOptions") ||
                d.ServiceType.Name.Contains("DbConnection")).ToList();

            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add a database context using an in-memory database for testing.
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForTesting");
            });

            // ─── Disable ALL background hosted services in tests ─────────────
            services.RemoveAll<IHostedService>();

            // ─── Replace Hangfire with in-memory storage ─────────────────────
            var hangfireDescriptors = services.Where(d =>
                d.ServiceType.FullName?.Contains("Hangfire") == true).ToList();
            foreach (var descriptor in hangfireDescriptors)
            {
                services.Remove(descriptor);
            }
            services.AddHangfire(config => config.UseInMemoryStorage());
            services.AddSingleton<IBackgroundJobClient>(sp =>
                new BackgroundJobClient(sp.GetRequiredService<JobStorage>()));

            // ─── Replace email services with no-op stubs ─────────────────────
            services.RemoveAll<IEmailService>();
            services.RemoveAll<IEmailQueueService>();
            services.RemoveAll<IEmailTemplateService>();
            services.AddTransient<IEmailService, NoOpEmailService>();
            services.AddScoped<IEmailQueueService, NoOpEmailQueueService>();
            services.AddSingleton<IEmailTemplateService, NoOpEmailTemplateService>();

            // Make sure the database schema is created
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

// ─── Test Stubs ──────────────────────────────────────────────────

internal class NoOpEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        => Task.CompletedTask;
}

internal class NoOpEmailQueueService : IEmailQueueService
{
    public string EnqueueEmail(string to, string subject, string htmlBody)
        => Guid.NewGuid().ToString();
}

internal class NoOpEmailTemplateService : IEmailTemplateService
{
    public Task<string> RenderTemplateAsync(string templateName, Dictionary<string, string> replacements)
        => Task.FromResult($"<p>Test email: {templateName}</p>");
}

