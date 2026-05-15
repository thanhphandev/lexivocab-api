using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Features.Settings;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Moq;
using Xunit;

namespace LexiVocab.UnitTests.Features.Settings;

public class SettingsHandlersTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IEncryptionService> _mockEncryption;
    private readonly Mock<IDateTimeProvider> _mockDateTime;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedNow = new(2026, 5, 15, 10, 0, 0);

    public SettingsHandlersTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockEncryption = new Mock<IEncryptionService>();
        _mockDateTime = new Mock<IDateTimeProvider>();

        _mockCurrentUser.Setup(x => x.UserId).Returns(_userId);
        _mockDateTime.Setup(x => x.UtcNow).Returns(_fixedNow);
        _mockEncryption.Setup(x => x.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
        _mockEncryption.Setup(x => x.Encrypt(It.IsAny<string>())).Returns<string>(s => $"ENC:{s}");
    }

    // ─── GetSettings ────────────────────────────────────────

    [Fact]
    public async Task GetSettings_WhenSettingsExist_ShouldReturnDto()
    {
        // Arrange
        var handler = new GetSettingsHandler(_mockUow.Object, _mockCurrentUser.Object, _mockEncryption.Object);
        var user = new User
        {
            Id = _userId,
            UserSetting = new UserSetting
            {
                DailyGoal = 30,
                HighlightColor = "#FF0000",
                TargetLanguage = "Japanese"
            }
        };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await handler.Handle(new GetSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.DailyGoal.Should().Be(30);
        result.Data.HighlightColor.Should().Be("#FF0000");
        result.Data.TargetLanguage.Should().Be("Japanese");
    }

    [Fact]
    public async Task GetSettings_WhenNoSettings_ShouldReturnDefaults()
    {
        // Arrange
        var handler = new GetSettingsHandler(_mockUow.Object, _mockCurrentUser.Object, _mockEncryption.Object);
        var user = new User { Id = _userId, UserSetting = null };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await handler.Handle(new GetSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.DailyGoal.Should().Be(20);        // default
        result.Data.TargetLanguage.Should().Be("English"); // default
    }

    [Fact]
    public async Task GetSettings_WhenUserNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var handler = new GetSettingsHandler(_mockUow.Object, _mockCurrentUser.Object, _mockEncryption.Object);
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        // Act
        var result = await handler.Handle(new GetSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    // ─── UpdateSettings ─────────────────────────────────────

    [Fact]
    public async Task UpdateSettings_WhenValid_ShouldApplyPartialUpdate()
    {
        // Arrange
        var handler = new UpdateSettingsHandler(_mockUow.Object, _mockCurrentUser.Object, _mockEncryption.Object, _mockDateTime.Object);
        var user = new User
        {
            Id = _userId,
            UserSetting = new UserSetting
            {
                DailyGoal = 20,
                TargetLanguage = "English",
                NativeLanguage = "Vietnamese"
            }
        };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Only update DailyGoal — other fields should remain unchanged
        var command = new UpdateSettingsCommand(
            null, null, null, DailyGoal: 50,
            null, null, null, null, null, null, null,
            null, null, null, null, null, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.UserSetting!.DailyGoal.Should().Be(50);
        user.UserSetting.TargetLanguage.Should().Be("English"); // unchanged
        user.UserSetting.UpdatedAt.Should().Be(_fixedNow);
    }

    [Fact]
    public async Task UpdateSettings_WhenNoSettingsExist_ShouldCreateNew()
    {
        // Arrange
        var handler = new UpdateSettingsHandler(_mockUow.Object, _mockCurrentUser.Object, _mockEncryption.Object, _mockDateTime.Object);
        var user = new User { Id = _userId, UserSetting = null };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var command = new UpdateSettingsCommand(
            IsHighlightEnabled: false,
            null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.UserSetting.Should().NotBeNull();
        user.UserSetting!.IsHighlightEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSettings_WithTelegramToken_ShouldEncryptBeforeSaving()
    {
        // Arrange
        var handler = new UpdateSettingsHandler(_mockUow.Object, _mockCurrentUser.Object, _mockEncryption.Object, _mockDateTime.Object);
        var user = new User { Id = _userId, UserSetting = new UserSetting() };
        _mockUow.Setup(x => x.Users.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var command = new UpdateSettingsCommand(
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, TelegramBotToken: "my-secret-token", null,
            null, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.UserSetting!.TelegramBotToken.Should().Be("ENC:my-secret-token");
        _mockEncryption.Verify(x => x.Encrypt("my-secret-token"), Times.Once);
    }
}
