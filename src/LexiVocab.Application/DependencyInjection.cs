using FluentValidation;
using LexiVocab.Application.Common.Behaviors;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LexiVocab.Application;

/// <summary>
/// Registers all Application layer services into the DI container.
/// Called from the API layer's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // MediatR — auto-discovers all IRequest/IRequestHandler in this assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Pipeline order: AuditLogging → Caching → Validation → Performance → Handler
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });

        // FluentValidation — auto-discovers all AbstractValidator<> in this assembly
        services.AddValidatorsFromAssembly(assembly);

        // Domain Services
        services.AddSingleton<ISrsAlgorithm, SrsAlgorithmService>();

        return services;
    }
}
