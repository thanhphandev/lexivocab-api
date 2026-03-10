using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Common.Interfaces;

public interface IPaymentServiceFactory
{
    IPaymentService GetService(PaymentProvider provider);
}
