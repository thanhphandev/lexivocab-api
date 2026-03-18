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
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── EF Core + PostgreSQL ─────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
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

        // ─── Data Seeders ─────────────────────────────────────
        services.AddScoped<IDataSeeder, FeatureDefinitionSeeder>();
        services.AddScoped<IDataSeeder, PlanDefinitionSeeder>();
        services.AddScoped<DbContextSeeder>();
        // ─── Background Jobs ──────────────────────────────────
        services.AddTransient<Services.ISubscriptionExpirationJob, Services.SubscriptionExpirationJob>();
        services.AddTransient<Services.IReviewReminderJob, Services.ReviewReminderJob>();
        services.AddTransient<Services.IMasterVocabularyUpdateJob, Services.MasterVocabularyUpdateJob>();
        services.AddTransient<Services.IPendingPaymentCleanupJob, Services.PendingPaymentCleanupJob>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ─── Email Services ────────────────────────────────
        services.AddTransient<IEmailService, Services.SmtpEmailService>();
        services.AddScoped<IEmailQueueService, Services.HangfireEmailQueueService>();
        services.AddSingleton<IEmailTemplateService, Services.EmailTemplateService>();

        // ─── Hangfire Integration (PostgreSQL backed) ───────────
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(configuration.GetConnectionString("DefaultConnection"))));

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
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        // ─── Dictionary Enrichment ───────────────────────────
        services.AddHttpClient<IDictionaryService, Services.FreeDictionaryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler();

        // ─── AI Services (Cloudflare Workers AI) ─────────────
        services.AddHttpClient<IAIService, Services.CloudflareAIService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .AddStandardResilienceHandler(options =>
        {
            // AI inference calls can take 10-30s; default 10s attempt timeout is too aggressive
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        });

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
        var redisConnection = configuration.GetConnectionString("Redis");
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
