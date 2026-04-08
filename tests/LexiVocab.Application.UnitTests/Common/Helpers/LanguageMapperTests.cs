using FluentAssertions;
using LexiVocab.Application.Common.Helpers;

namespace LexiVocab.Application.UnitTests.Common.Helpers;

public class LanguageMapperTests
{
    [Fact]
    public void GetName_WithMissingCode_ShouldReturnVietnameseFallback()
    {
        var result = LanguageMapper.GetName(null);

        result.Should().Be("Vietnamese");
    }

    [Fact]
    public void GetName_WithAutoSource_ShouldReturnSourceLanguagePlaceholder()
    {
        var result = LanguageMapper.GetName("auto", isSource: true);

        result.Should().Be("the source language");
    }

    [Fact]
    public void GetName_ShouldBeCaseInsensitiveForKnownCodes()
    {
        var result = LanguageMapper.GetName("EN");

        result.Should().Be("English");
    }

    [Fact]
    public void GetName_WithUnknownCode_ShouldEchoInput()
    {
        var result = LanguageMapper.GetName("xx");

        result.Should().Be("xx");
    }
}
