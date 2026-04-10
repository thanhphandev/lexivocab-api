using LexiVocab.API.Middlewares;
using LexiVocab.Application;
using LexiVocab.Infrastructure;
using LexiVocab.Infrastructure.Persistence;
using LexiVocab.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.ResponseCompression;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.Dashboard;
using Asp.Versioning;

// ────────────────────────────────────────────────────────────────
// Bootstrap Serilog (before anything else can crash)
// ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/lexivocab-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 100_000_000,
        rollOnFileSizeLimit: true)
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:5341/ingest/otlp/v1/logs";
        options.Protocol = OtlpProtocol.HttpProtobuf;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "lexivocab-api"
        };
    })
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

    // ─── Health Checks ────────────────────────────────────────
    var healthChecks = builder.Services.AddHealthChecks();
    
    // Diagnostic log for Cloud Deployment
    var rawDbUrl = Environment.GetEnvironmentVariable("DATABASE_URL") ?? builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(rawDbUrl))
    {
        var displayHost = "Unknown/URI";
        if (rawDbUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        {
            var parts = rawDbUrl.Split(';');
            var hostPart = parts.FirstOrDefault(p => p.StartsWith("Host=", StringComparison.OrdinalIgnoreCase));
            displayHost = hostPart?.Split('=')[1] ?? displayHost;
        }
        else if (rawDbUrl.Contains("@"))
        {
            displayHost = rawDbUrl.Split('@')[1].Split('/')[0].Split(':')[0];
        }
        Log.Information("🏗️ Database resolved. Host: {Host}, Environment: {Env}", displayHost, builder.Environment.EnvironmentName);
        healthChecks.AddNpgSql(rawDbUrl);
    }
        
    var rawRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(rawRedisUrl))
    {
        Log.Information("🏗️ Redis resolved (Source: {Source})", Environment.GetEnvironmentVariable("REDIS_URL") != null ? "REDIS_URL" : "Config");
        healthChecks.AddRedis(rawRedisUrl);
    }

    // ─── Controllers ──────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // ─── API Versioning ───────────────────────────────────────
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("x-api-version"),
            new QueryStringApiVersionReader("api-version")
        );
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // ─── Swagger / OpenAPI ────────────────────────────────────
    builder.Services.AddOpenApi(); // Supports OpenAPI 3.0/3.1

    // ─── CORS (Extension + Web Dashboard) ─────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("LexiVocabPolicy", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(_ => true) // Allow any origin in dev (e.g., wikipedia.org -> localhost)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
            else
            {
                var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? ["https://lexivocab.store"];
                
                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
        });
    });

    // ─── Rate Limiting (IP-based, PartitionedRateLimiter) ─────
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Return structured JSON response on rate limit rejection
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            var response = JsonSerializer.Serialize(new
            {
                success = false,
                error = "Too many requests. Please slow down.",
                statusCode = 429,
                retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? retryAfter.TotalSeconds : 60
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await context.HttpContext.Response.WriteAsync(response, cancellationToken);
        };

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

        // Strict: login/register/forgot-password — chống brute-force (5 req/min)
        options.AddPolicy("AuthStrictLimit", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0 // Reject immediately
                }));

        // Lenient: /me, /permissions, profile reads — nhiều tab/client gọi liên tục (60 req/min)
        options.AddPolicy("UserReadLimit", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5
                }));

        // Refresh token: multi-tab bursting, nhưng vẫn có giới hạn (30 req/min)
        options.AddPolicy("RefreshLimit", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        // Sensitive writes: đổi password, đổi email, xóa tài khoản (10 req/min)
        options.AddPolicy("SensitiveWriteLimit", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));
    });

    // ─── Response Compression ─────────────────────────────────
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.SmallestSize);

    // ─── Kestrel Hardening ────────────────────────────────────
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB max request body
    });

    // ────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Hangfire Recurring Jobs ──────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        recurringJobManager.AddOrUpdate<ISubscriptionExpirationJob>(
            "SubscriptionExpirationJob",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Daily(0)); // Run daily at midnight UTC
            
        recurringJobManager.AddOrUpdate<IReviewReminderJob>(
            "ReviewReminderJob",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Daily(1)); // Run daily at 1 AM UTC

        recurringJobManager.AddOrUpdate<IPendingPaymentCleanupJob>(
            "PendingPaymentCleanupJob",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Minutely()); // Sweep pending tx frequently (expiry/auto-cancel)
    }

    // ─── Middleware Pipeline ──────────────────────────────────
    // Order matters: Exception → Security Headers → HTTPS → CORS → RateLimit → Auth → Controllers
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // Support Chrome's Private Network Access preflight requests for localhost testing from public websites
    app.Use(async (context, next) =>
    {
        if (context.Request.Headers.TryGetValue("Access-Control-Request-Private-Network", out var isPrivate) && isPrivate == "true")
        {
            context.Response.Headers.Append("Access-Control-Allow-Private-Network", "true");
        }
        await next();
    });

    app.UseCors("LexiVocabPolicy");
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

    app.UseResponseCompression();
    app.UseSerilogRequestLogging();
    
    // In Docker behind a reverse proxy (or local dev), HTTPS is handled by the host/load balancer.
    // app.UseHttpsRedirection(); 

    // app.UseCors("LexiVocabPolicy"); // Moved earlier in the pipeline
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    
    // Hangfire Dashboard — restricted to Admin users in production
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        DashboardTitle = "LexiVocab Email Queue",
        Authorization = new Hangfire.Dashboard.IDashboardAuthorizationFilter[]
        {
            new HangfireAdminAuthorizationFilter(app.Environment.IsDevelopment())
        }
    });

    app.MapControllers();

    // ─── Auto-Migrate & Seed Database ──────────────────────────────
    var runMigrations = builder.Configuration.GetValue<bool>("RUN_MIGRATIONS", false);
    if (app.Environment.IsDevelopment() || runMigrations)
    {
        using var scope = app.Services.CreateScope();
        
        // Call the data seeder to migrate and populate default features and plans
        var seeder = scope.ServiceProvider.GetRequiredService<LexiVocab.Infrastructure.Persistence.Seeding.DbContextSeeder>();
        await seeder.SeedAllAsync();
        Log.Information("✅ Database initialization and seeding completed (Mode: {Mode}).", 
            runMigrations ? "Production-Override" : "Development");
    }

    // ─── Health Check Endpoint ────────────────────────────────
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
            });
            await context.Response.WriteAsync(result);
        }
    });

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
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }

/// <summary>
/// Hangfire Dashboard authorization filter.
/// Development: allows any authenticated user.
/// Production: requires Admin role.
/// </summary>
public class HangfireAdminAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    private readonly bool _isDevelopment;

    public HangfireAdminAuthorizationFilter(bool isDevelopment)
    {
        _isDevelopment = isDevelopment;
    }

    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        // In development, allow all access (same as LocalRequestsOnly before)
        if (_isDevelopment)
            return true;

        // In production, require authenticated Admin user
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("Admin");
    }
}
