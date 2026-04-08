using FluentAssertions;
using LexiVocab.Application.Common;

namespace LexiVocab.Application.UnitTests.Common;

public class PagedResultTests
{
    [Fact]
    public void PaginationMetadata_ShouldBeCalculatedCorrectly()
    {
        var result = new PagedResult<int>
        {
            Items = [1, 2, 3],
            TotalCount = 25,
            Page = 2,
            PageSize = 10
        };

        result.TotalPages.Should().Be(3);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void EmptyFirstPage_ShouldNotExposeNavigation()
    {
        var result = new PagedResult<int>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 10
        };

        result.TotalPages.Should().Be(0);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }
}
