using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LexiVocab.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs slow requests (> 500ms) for performance monitoring.
/// </summary>
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private const int SlowRequestThresholdMs = 1000;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            var requestName = typeof(TRequest).Name;

            // Simple reflection to extract properties safely without exposing sensitive info
            var safePayload = new Dictionary<string, object?>();
            foreach (var prop in typeof(TRequest).GetProperties())
            {
                var name = prop.Name;
                if (name.Contains("Password", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("Token", StringComparison.OrdinalIgnoreCase))
                {
                    safePayload[name] = "***MASKED***";
                }
                else
                {
                    safePayload[name] = prop.GetValue(request);
                }
            }

            _logger.LogWarning(
                "⚠️ Slow Request: {RequestName} took {ElapsedMs}ms — Payload: {@RequestPayload}",
                requestName,
                sw.ElapsedMilliseconds,
                safePayload);
        }

        return response;
    }
}
