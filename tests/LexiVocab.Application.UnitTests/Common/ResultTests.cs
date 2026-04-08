using FluentAssertions;
using LexiVocab.Application.Common;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Models;

namespace LexiVocab.Application.UnitTests.Common;

public class ResultTests
{
    [Fact]
    public void GenericSuccess_ShouldPopulateSuccessState()
    {
        var result = Result<string>.Success("ok");

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("ok");
        result.StatusCode.Should().Be(200);
        result.Error.Should().BeNull();
        result.ErrorCode.Should().Be(ErrorCode.UNKNOWN_ERROR);
    }

    [Fact]
    public void GenericFailure_ShouldPreserveMetadata()
    {
        var details = new ErrorDetails
        {
            ReferenceId = "trace-123",
            RetryAfterSeconds = 30
        };

        var result = Result<string>.Failure("invalid", 422, ErrorCode.VALIDATION_FAILED, details);

        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Error.Should().Be("invalid");
        result.StatusCode.Should().Be(422);
        result.ErrorCode.Should().Be(ErrorCode.VALIDATION_FAILED);
        result.Details.Should().BeSameAs(details);
    }

    [Fact]
    public void NonGenericConflict_ShouldReturnConflictShape()
    {
        var result = Result.Conflict("duplicate");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("duplicate");
        result.StatusCode.Should().Be(409);
        result.ErrorCode.Should().Be(ErrorCode.RESOURCE_CONFLICT);
    }
}
