using Hangfire;
using Hangfire.Common;
using Hangfire.InMemory;
using Hangfire.States;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace LexiVocab.IntegrationTests.Base;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithDatabase("lexivocab_test")
        .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "trust")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _dbContainer.StartAsync().GetAwaiter().GetResult();
        builder.UseEnvironment("Testing");

        // Force the application to use the Testcontainers connection string
        builder.UseSetting("ConnectionStrings:DefaultConnection", _dbContainer.GetConnectionString());
        builder.UseSetting("DATABASE_URL", _dbContainer.GetConnectionString());

        builder.ConfigureServices(services =>
        {
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
 
            // ─── Replace Redis with In-Memory Cache ──────────────────────────
            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbContainer.DisposeAsync().GetAwaiter().GetResult();
        }
        base.Dispose(disposing);
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

