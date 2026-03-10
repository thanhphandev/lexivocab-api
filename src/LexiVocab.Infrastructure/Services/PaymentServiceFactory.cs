using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LexiVocab.Infrastructure.Services;

public class PaymentServiceFactory : IPaymentServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PaymentServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPaymentService GetService(PaymentProvider provider)
    {
        var services = _serviceProvider.GetServices<IPaymentService>();
        return services.FirstOrDefault(s => s.Provider == provider) 
               ?? throw new NotSupportedException($"Payment provider {provider} is not supported.");
    }
}
