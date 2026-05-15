using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Application.Features.Vocabularies.Queries;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Vocabularies.Queries;

public class ExportVocabulariesHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IFeatureGatingService> _mockFeatureGating;
    private readonly Mock<IDateTimeProvider> _mockDateTime;
    private readonly ExportVocabulariesHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedNow = new(2026, 5, 15, 10, 0, 0);

    public ExportVocabulariesHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockFeatureGating = new Mock<IFeatureGatingService>();
        _mockDateTime = new Mock<IDateTimeProvider>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _mockDateTime.Setup(x => x.UtcNow).Returns(_fixedNow);

        _handler = new ExportVocabulariesHandler(
            _mockUow.Object, _mockCurrentUser.Object,
            _mockFeatureGating.Object, _mockDateTime.Object);
    }

    private void SetupPremiumUser()
    {
        var permissions = new UserPermissionsDto("Pro", 1000, null, new Dictionary<string, string> { ["EXPORT_ANKI"] = "true" });
        _mockFeatureGating.Setup(x => x.GetPermissionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);
    }

    private void SetupVocabularies(params UserVocabulary[] vocabs)
    {
        _mockUow.Setup(x => x.Vocabularies.GetByUserIdAsync(
                _userId, 1, int.MaxValue, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((vocabs.ToList(), vocabs.Length));
    }

    [Fact]
    public async Task Handle_WhenNotPremium_ShouldReturnForbidden()
    {
        // Arrange
        var permissions = new UserPermissionsDto("Free", 50, null, new Dictionary<string, string>());
        _mockFeatureGating.Setup(x => x.GetPermissionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        // Act
        var result = await _handler.Handle(new ExportVocabulariesQuery("json"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Handle_JsonFormat_ShouldReturnJsonFile()
    {
        // Arrange
        SetupPremiumUser();
        SetupVocabularies(
            new UserVocabulary { WordText = "hello", CustomMeaning = "xin chào", CreatedAt = _fixedNow }
        );

        // Act
        var result = await _handler.Handle(new ExportVocabulariesQuery("json"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ContentType.Should().Be("application/json");
        result.Data.FileName.Should().Be("lexivocab_export_20260515.json");
        result.Data.Bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_CsvFormat_ShouldReturnCsvFileWithBom()
    {
        // Arrange
        SetupPremiumUser();
        SetupVocabularies(
            new UserVocabulary { WordText = "test", CustomMeaning = "kiểm tra", CreatedAt = _fixedNow }
        );

        // Act
        var result = await _handler.Handle(new ExportVocabulariesQuery("csv"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ContentType.Should().Be("text/csv");
        result.Data.FileName.Should().Be("lexivocab_export_20260515.csv");
        // Check UTF-8 BOM
        result.Data.Bytes[0].Should().Be(0xEF);
        result.Data.Bytes[1].Should().Be(0xBB);
        result.Data.Bytes[2].Should().Be(0xBF);
    }

    [Fact]
    public async Task Handle_QuizletFormat_ShouldReturnTabSeparatedFile()
    {
        // Arrange
        SetupPremiumUser();
        SetupVocabularies(
            new UserVocabulary { WordText = "run", CustomMeaning = "chạy", CreatedAt = _fixedNow }
        );

        // Act
        var result = await _handler.Handle(new ExportVocabulariesQuery("quizlet"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ContentType.Should().Be("text/plain");
        result.Data.FileName.Should().Contain("quizlet");
        var content = System.Text.Encoding.UTF8.GetString(result.Data.Bytes.Skip(3).ToArray()); // skip BOM
        content.Should().Contain("run\tchạy");
    }

    [Fact]
    public async Task Handle_ShouldUseDeterministicTimestamp()
    {
        // Arrange
        SetupPremiumUser();
        SetupVocabularies();

        // Act
        var result = await _handler.Handle(new ExportVocabulariesQuery("json"), CancellationToken.None);

        // Assert — filename uses controlled date, not system clock
        result.Data!.FileName.Should().Contain("20260515");
    }
}
