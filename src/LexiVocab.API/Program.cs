using LexiVocab.API.Middlewares;
using LexiVocab.Application;
using LexiVocab.Infrastructure;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

// ────────────────────────────────────────────────────────────────
// Bootstrap Serilog (before anything else can crash)
// ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/lexivocab-.log", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .CreateLogger();

try
{
    Log.Information("🚀 Starting LexiVocab API...");

    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog Host Integration ─────────────────────────────
    builder.Host.UseSerilog();

    // ─── Application & Infrastructure DI Registration ─────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ─── Controllers ──────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // ─── Swagger / OpenAPI ────────────────────────────────────
    builder.Services.AddOpenApi();

    // ─── CORS (Extension + Web Dashboard) ─────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("LexiVocabPolicy", policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000", "http://localhost:5173", "chrome-extension://*"];

            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // ─── Rate Limiting (IP-based, PartitionedRateLimiter) ─────
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Global: 100 requests per minute per IP
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                }));

        // Auth endpoints: strict 5 requests per minute per IP (anti brute-force)
        options.AddPolicy("AuthLimit", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0 // No queueing — reject immediately
                }));
    });

    // ─── Kestrel Hardening ────────────────────────────────────
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false; // Hide "Server: Kestrel" from responses
    });

    // ────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Middleware Pipeline ──────────────────────────────────
    // Order matters: Exception → Security Headers → HTTPS → CORS → RateLimit → Auth → Controllers
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // HSTS: enforce HTTPS via browser cache (Production only)
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(); // Beautiful API testing UI at /scalar/v1
    }

    // Support reverse proxy (Nginx, Azure, AWS ALB) — trust X-Forwarded-For headers
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                           Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    });

    app.UseSerilogRequestLogging();
    
    // In Docker behind a reverse proxy (or local dev), HTTPS is handled by the host/load balancer.
    // app.UseHttpsRedirection(); 

    app.UseCors("LexiVocabPolicy");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // ─── Auto-Migrate Database in Development ─────────────────
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("✅ Database migration applied.");
    }

    // ─── Health Check Endpoint ────────────────────────────────
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        version = "1.0.0"
    }));

    // Root endpoint to prevent 404 on base URL pings
    app.MapGet("/", () => Results.Ok(new
    {
        app = "LexiVocab API",
        docs = "/scalar/v1",
        version = "1.0.0"
    }));

    Log.Information("✅ LexiVocab API is running!");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
