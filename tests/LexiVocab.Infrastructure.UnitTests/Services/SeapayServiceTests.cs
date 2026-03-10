using FluentAssertions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LexiVocab.Infrastructure.UnitTests.Services;

public class SeapayServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<IEmailTemplateService> _mockTemplate;
    private readonly Mock<IEmailQueueService> _mockEmailQueue;
    private readonly Mock<ILogger<SeapayService>> _mockLogger;
    private readonly SeapayService _service;

    public SeapayServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockConfig = new Mock<IConfiguration>();
        _mockTemplate = new Mock<IEmailTemplateService>();
        _mockEmailQueue = new Mock<IEmailQueueService>();
        _mockLogger = new Mock<ILogger<SeapayService>>();

        _mockConfig.Setup(x => x["Seapay:ApiKey"]).Returns("test-key");
        
        _service = new SeapayService(
            _mockUow.Object,
            _mockConfig.Object,
            _mockTemplate.Object,
            _mockEmailQueue.Object,
            _mockLogger.Object);
    }

    [Theory]
    [InlineData("Payment for ORDER LV123", "LV123")]
    [InlineData("LEXIVOCAB_LV999_PAY", "LV999")]
    [InlineData("No reference here", null)]
    public void ExtractReference_ShouldIdentifyCorrectCode(string content, string? expected)
    {
        // Act
        // Note: SeapayService.ExtractReference is private, but we can test via reflection 
        // Or refactor to internal/public. For unit tests, testing the behavior of the webhook 
        // process is better. But for precision, let's use reflection once to be sure.
        
        var method = typeof(SeapayService).GetMethod("ExtractReference", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (string?)method!.Invoke(_service, new object[] { content });

        // Assert
        result.Should().Be(expected);
    }
}
