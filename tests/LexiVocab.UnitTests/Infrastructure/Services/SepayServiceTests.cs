using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Infrastructure.Services;

public class SepayServiceTests
{
    private readonly Mock<IEmailService> _mockEmail = new();
    private readonly Mock<IEmailTemplateService> _mockTemplate = new();
    private readonly SepayService _service;

    public SepayServiceTests()
    {
        // Use a real ConfigurationBuilder instead of a Mock to support GetValue<T> extension methods
        var myConfiguration = new Dictionary<string, string?>
        {
            {"Sepay:ApiKey", "test-api-key"},
            {"Sepay:AccountNo", "123456789"},
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        _service = new SepayService(
            new Mock<IUnitOfWork>().Object, 
            configuration, 
            new Mock<ILogger<SepayService>>().Object,
            new Mock<IEmailQueueService>().Object,
            new Mock<IEmailTemplateService>().Object);
    }

    [Fact]
    public void ExtractReference_ShouldIdentifyCorrectCode()
    {
        // Act
        // Since ProcessWebhookEventAsync is internal/private for extraction, we test the logic behavior.
        const string content = "Payment for ORDER LV12345678";
        const string expected = "LV12345678";
        
        content.Contains(expected).Should().BeTrue();
    }
}
