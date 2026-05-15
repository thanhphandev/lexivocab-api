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
using StackExchange.Redis;
using Microsoft.AspNetCore.DataProtection;
using RedisRateLimiting;
// ────────────────────────────────────────────────────────────────
// Bootstrap Serilog (before anything else can crash)
// ────────────────────────────────────────────────────────────────
var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

var logConfig = new LoggerConfiguration()
    .WriteTo.Console()
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
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning);

if (isProduction)
{
    // Production: Console + OTEL only. File sink is excluded because containers are ephemeral
    // and writing to disk increases I/O pressure + risks filling container tmp storage.
    logConfig.MinimumLevel.Warning();
}
else
{
    // Development/Staging: Include file sink for local debugging convenience
    logConfig.MinimumLevel.Information()
        .WriteTo.File("logs/lexivocab-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            fileSizeLimitBytes: 100_000_000,
            rollOnFileSizeLimit: true);
}

Log.Logger = logConfig.CreateLogger();

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


    var rawDbConnectionString = (builder.Configuration.GetConnectionString("DefaultConnection") 
                                 ?? builder.Configuration["DATABASE_URL"])?.Trim('"');
    var dbConnectionString = ConnectionStringParser.ParseDatabaseUrl(rawDbConnectionString ?? "");
    
    var healthChecks = builder.Services.AddHealthChecks();
    
    if (!string.IsNullOrEmpty(dbConnectionString))
    {
        Log.Information("🏗️ Database health check registered.");
        healthChecks.AddNpgSql(dbConnectionString);
    }
        
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
                                ?? builder.Configuration["REDIS_URL"];
    if (!string.IsNullOrWhiteSpace(redisConnectionString))
    {
        Log.Information("🏗️ Redis health check registered.");
        healthChecks.AddRedis(redisConnectionString);
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
            policy.SetIsOriginAllowed(_ => true) // Allow any origin because the extension operates on all websites
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // Cached options to avoid per-request GC allocation
    var rateLimitJsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ─── Rate Limiting (IP-based, Distributed via Redis) ─────
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
            }, rateLimitJsonOptions);
            await context.HttpContext.Response.WriteAsync(response, cancellationToken);
        };

        RateLimitPartition<string> CreatePartition(HttpContext context, string policyName, int permitLimit, TimeSpan window)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
            var partitionKey = $"RateLimit:{policyName}:{ip}";
            var multiplexer = context.RequestServices.GetService<IConnectionMultiplexer>();

            if (multiplexer != null)
            {
                return RedisRateLimitPartition.GetFixedWindowRateLimiter(
                    partitionKey,
                    _ => new RedisFixedWindowRateLimiterOptions
                    {
                        ConnectionMultiplexerFactory = () => multiplexer,
                        PermitLimit = permitLimit,
                        Window = window
                    });
            }
            
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }

        // Global: 100 requests per minute per IP
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => 
            CreatePartition(context, "Global", 100, TimeSpan.FromMinutes(1)));

        // Strict: login/register/forgot-password (5 req/min)
        options.AddPolicy("AuthStrictLimit", context => CreatePartition(context, "AuthStrict", 5, TimeSpan.FromMinutes(1)));

        // Lenient: /me, profile reads (60 req/min)
        options.AddPolicy("UserReadLimit", context => CreatePartition(context, "UserRead", 60, TimeSpan.FromMinutes(1)));

        // Refresh token: multi-tab bursting (30 req/min)
        options.AddPolicy("RefreshLimit", context => CreatePartition(context, "Refresh", 30, TimeSpan.FromMinutes(1)));

        // Sensitive writes: đổi password, email, xóa tài khoản (10 req/min)
        options.AddPolicy("SensitiveWriteLimit", context => CreatePartition(context, "SensitiveWrite", 10, TimeSpan.FromMinutes(1)));
    });

    // ─── Data Protection (Distributed via Redis) ─────────────
    // Resolves: "Storing keys in a directory that may not be persisted"
    if (!string.IsNullOrWhiteSpace(redisConnectionString))
    {
        try 
        {
            // Skip Redis DataProtection in Testing environment
            if (!builder.Environment.IsEnvironment("Testing"))
            {
                var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
                redisOptions.AbortOnConnectFail = false;
                
                var multiplexer = ConnectionMultiplexer.Connect(redisOptions);
                if (multiplexer.IsConnected)
                {
                    Log.Information("✅ DataProtection successfully connected to Redis.");
                }
                else
                {
                    Log.Warning("⚠️ DataProtection Redis connection is currently unreachable. StackExchange.Redis will keep retrying in the background. (AbortOnConnectFail=false)");
                }

                builder.Services.AddDataProtection()
                    .PersistKeysToStackExchangeRedis(multiplexer, "LexiVocab-DataProtection-Keys");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Could not connect to Redis for DataProtection. Falling back to ephemeral storage.");
        }
    }

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

    // ─── Graceful Shutdown ────────────────────────────────────
    // Give in-flight requests and Hangfire jobs time to finish before forced termination.
    builder.Services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(30);
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
    // Order matters: ForwardedHeaders → Exception → CORS → Security → RateLimit → Auth → Controllers

    // CRITICAL: ForwardedHeaders MUST be first so Rate Limiting gets the real client IP
    // (not the proxy/load balancer IP). Without this, in Azure Container Apps,
    // ALL requests appear from the same internal IP → rate limits break.
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                           Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    };
    
    // Clear KnownNetworks and KnownProxies so we trust the headers from any internal Envoy/Proxy
    // typical in distributed environments like Azure Container Apps.
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();

    app.UseForwardedHeaders(forwardedHeadersOptions);

    app.UseMiddleware<GlobalExceptionMiddleware>();

    // Private Network Access middleware: Only needed for local development
    // where browser extensions on public websites access localhost API
    if (app.Environment.IsDevelopment())
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue("Access-Control-Request-Private-Network", out var isPrivate) && isPrivate == "true")
            {
                context.Response.Headers.Append("Access-Control-Allow-Private-Network", "true");
            }
            await next();
        });
    }

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

    app.UseResponseCompression();
    app.UseSerilogRequestLogging();

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
    // Uses a PostgreSQL advisory lock to ensure only ONE instance runs migrations at a time.
    // This prevents race conditions when multiple Container App replicas start simultaneously.
    var runMigrations = builder.Configuration.GetValue<bool>("RUN_MIGRATIONS", false);
    var isTesting = app.Environment.IsEnvironment("Testing");
    if ((app.Environment.IsDevelopment() || runMigrations) && !isTesting)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const long migrationLockId = 20260412; // Arbitrary unique ID for advisory lock
        
        if (db.Database.IsRelational())
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            await using var lockCmd = conn.CreateCommand();
            lockCmd.CommandText = $"SELECT pg_try_advisory_lock({migrationLockId})";
            var acquired = (bool)(await lockCmd.ExecuteScalarAsync())!;

            if (acquired)
            {
                try
                {
                    var seeder = scope.ServiceProvider.GetRequiredService<LexiVocab.Infrastructure.Persistence.Seeding.DbContextSeeder>();
                    await seeder.SeedAllAsync();
                    Log.Information("✅ Database initialization and seeding completed (Mode: {Mode}).",
                        runMigrations ? "Production-Override" : "Development");
                }
                finally
                {
                    await using var unlockCmd = conn.CreateCommand();
                    unlockCmd.CommandText = $"SELECT pg_advisory_unlock({migrationLockId})";
                    await unlockCmd.ExecuteScalarAsync();
                }
            }
            else
            {
                Log.Information("⏳ Another instance is running migrations. Skipping on this replica.");
            }
        }
        else 
        {
            // For non-relational providers (like SQLite in-memory in tests), just seed directly
            var seeder = scope.ServiceProvider.GetRequiredService<LexiVocab.Infrastructure.Persistence.Seeding.DbContextSeeder>();
            await seeder.SeedAllAsync();
            Log.Information("✅ Database seeded (Non-relational provider).");
        }
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
        version = "1.0.0",
        status = "healthy"
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
