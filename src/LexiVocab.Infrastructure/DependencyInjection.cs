using System.Text;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Authentication;
using LexiVocab.Infrastructure.Persistence;
using LexiVocab.Infrastructure.Persistence.Seeding;
using LexiVocab.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace LexiVocab.Infrastructure;

/// <summary>
/// Registers all Infrastructure layer services into the DI container.
/// Called from the API layer's Program.cs.
/// </summary>
public static class DependencyInjection
{
    private static string NormalizePostgresConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;
        
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && 
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        try
        {
            var uri = new Uri(connectionString);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : string.Empty;
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');

            // Reconstruct as standard Npgsql Key=Value string compatible with all providers (Hangfire, etc)
            return $"Host={host};Port={port};Database={database};Username={user};Password={password};Pooling=true;Maximum Pool Size=100;SslMode=Prefer;Trust Server Certificate=true;";
        }
        catch
        {
            return connectionString;
        }
    }

    private static string GetDbConnectionString(IConfiguration configuration)
    {
        // 1. Try direct DATABASE_URL (Railway/Heroku default)
        var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(dbUrl)) return NormalizePostgresConnectionString(dbUrl);

        // 2. Try ConnectionStrings:DefaultConnection
        var connStr = configuration.GetConnectionString("DefaultConnection") 
               ?? throw new InvalidOperationException("Database connection string not found.");
        
        return NormalizePostgresConnectionString(connStr);
    }

    private static string? GetRedisConnectionString(IConfiguration configuration)
    {
        // 1. Try direct REDIS_URL
        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
        if (!string.IsNullOrWhiteSpace(redisUrl)) return redisUrl;

        // 2. Try ConnectionStrings:Redis
        return configuration.GetConnectionString("Redis");
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── EF Core + PostgreSQL ─────────────────────────────
        var dbConnectionString = GetDbConnectionString(configuration);
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                dbConnectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
        });

        // ─── Repositories & Unit of Work ──────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVocabularyRepository, VocabularyRepository>();
        services.AddScoped<IReviewLogRepository, ReviewLogRepository>();
        services.AddScoped<IMasterVocabularyRepository, MasterVocabularyRepository>();
        services.AddScoped<IVocabTagRepository, VocabTagRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<IPlanDefinitionRepository, PlanDefinitionRepository>();
        services.AddScoped<IPlanPricingRepository, PlanPricingRepository>();
        services.AddScoped<IFeatureDefinitionRepository, FeatureDefinitionRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();

        // ─── Data Seeders ─────────────────────────────────────
        services.AddScoped<IDataSeeder, UserSeeder>();
        services.AddScoped<IDataSeeder, FeatureDefinitionSeeder>();
        services.AddScoped<IDataSeeder, PlanDefinitionSeeder>();
        services.AddScoped<IDataSeeder, MasterVocabularySeeder>();
        services.AddScoped<DbContextSeeder>();
        // ─── Background Jobs ──────────────────────────────────
        services.AddTransient<Services.ISubscriptionExpirationJob, Services.SubscriptionExpirationJob>();
        services.AddTransient<Services.IReviewReminderJob, Services.ReviewReminderJob>();
        services.AddTransient<Services.IPendingPaymentCleanupJob, Services.PendingPaymentCleanupJob>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ─── Email Services ────────────────────────────────
        var resendApiKey = configuration["Resend:ApiKey"] ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");
        if (!string.IsNullOrEmpty(resendApiKey))
        {
            services.AddHttpClient("ResendClient", client => {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler();
            services.AddTransient<IEmailService, Services.ResendEmailService>();
        }
        else
        {
            services.AddTransient<IEmailService, Services.SmtpEmailService>();
        }
        
        services.AddScoped<IEmailQueueService, Services.HangfireEmailQueueService>();
        services.AddSingleton<IEmailTemplateService, Services.EmailTemplateService>();

        // ─── External Notification Services ──────────────────
        services.AddHttpClient<ITelegramNotificationService, Services.Notifications.TelegramNotificationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler();

        services.AddHttpClient<IZaloNotificationService, Services.Notifications.ZaloNotificationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler();

        // ─── Hangfire Integration (PostgreSQL backed) ───────────
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(dbConnectionString)));

        // Add the processing server as IHostedService
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount * 5; // e.g., 20+ concurrent jobs
        });

        // ─── Audit Logging ────────────────────────────────────
        services.AddScoped<IAuditLogService, Services.AuditLogService>();

        // ─── Authentication (JWT Bearer) ──────────────────────
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpContextAccessor();
        
        // ─── Freemium & Payments ─────────────────────────────
        services.AddScoped<IFeatureGatingService, Services.FeatureGatingService>();
        services.AddScoped<IPaymentServiceFactory, Services.PaymentServiceFactory>();
        
        services.AddHttpClient<IPaymentService, Services.PayPalService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddStandardResilienceHandler();

        services.AddScoped<IPaymentService, Services.SepayService>();

        // ─── Google OAuth ─────────────────────────────────────
        services.AddScoped<IEncryptionService, Services.EncryptionService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        // ─── Dictionary Enrichment ───────────────────────────
        services.AddHttpClient<IDictionaryService, Services.FreeDictionaryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler();

        // ─── Unified AI System (LLMs & Orchestration) ─────────────
        services.AddSingleton<IPromptTemplateService, Services.AI.PromptTemplateService>();
        services.AddScoped<IAIOrchestratorService, Services.AI.AIOrchestratorService>();

        Action<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions> aiResilienceOptions = options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        };

        services.AddHttpClient<ILLMProvider, Services.AI.Providers.OpenAiCompatibleLLMProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        }).AddStandardResilienceHandler(aiResilienceOptions);



        // ─── Translation Streaming Strategies ─────────────
        services.AddScoped<ITranslationStreamService, Services.Translation.TranslationStreamService>();

        services.AddScoped<Services.Translation.Providers.ITranslationProvider, Services.Translation.Providers.LlmTranslationProvider>();

        services.AddHttpClient<Services.Translation.Providers.ITranslationProvider, Services.Translation.Providers.GoogleTranslationProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).AddStandardResilienceHandler();

        services.AddHttpClient<Services.Translation.Providers.ITranslationProvider, Services.Translation.Providers.LingvaTranslationProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).AddStandardResilienceHandler();

        services.AddScoped<Services.Translation.Providers.ITranslationProvider, Services.Translation.Providers.BingTranslationProvider>();

        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero // No clock skew for precise token expiry
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var cache = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                        var userIdClaim = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) 
                                       ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                                       
                        if (userIdClaim != null)
                        {
                            var isDeactivated = await cache.GetStringAsync($"user:deactivated:{userIdClaim.Value}");
                            if (!string.IsNullOrEmpty(isDeactivated))
                            {
                                context.Fail("User account is deactivated or deleted.");
                            }
                        }
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            // Policies are intentionally kept minimal as we use dynamic feature gating
        });

        // ─── Redis Distributed Cache ──────────────────────────
        var redisConnection = GetRedisConnectionString(configuration);
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "LexiVocab:";
            });

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnection));
        }
        else
        {
            // Fallback to in-memory cache for development without Redis
            services.AddDistributedMemoryCache();
        }

        return services;
    }
}
