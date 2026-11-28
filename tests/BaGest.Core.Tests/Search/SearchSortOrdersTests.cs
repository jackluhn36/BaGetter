using System;
using Xunit;

namespace BaGest.Core.Tests.Search;

public class SearchSortOrdersTests
{
    [Theory]
    [InlineData("relevance", SearchSortOrder.Relevance)]
    [InlineData("lastEdited", SearchSortOrder.LastEdited)]
    [InlineData("published", SearchSortOrder.Published)]
    [InlineData("title-asc", SearchSortOrder.TitleAscending)]
    [InlineData("title-desc", SearchSortOrder.TitleDescending)]
    [InlineData("created-asc", SearchSortOrder.CreatedAscending)]
    [InlineData("created-desc", SearchSortOrder.CreatedDescending)]
    [InlineData("totalDownloads-asc", SearchSortOrder.TotalDownloadsAscending)]
    [InlineData("totalDownloads-desc", SearchSortOrder.TotalDownloadsDescending)]
    public void Parse_KnownValue_ReturnsOrder(string value, SearchSortOrder expected)
    {
        Assert.Equal(expected, SearchSortOrders.Parse(value));
    }

    [Theory]
    [InlineData("TOTALDOWNLOADS-DESC", SearchSortOrder.TotalDownloadsDescending)]
    [InlineData("Title-Asc", SearchSortOrder.TitleAscending)]
    public void Parse_IgnoresCase(string value, SearchSortOrder expected)
    {
        Assert.Equal(expected, SearchSortOrders.Parse(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("downloads")]
    [InlineData("totalDownloads")]
    public void Parse_MissingOrUnknownValue_ReturnsRelevance(string value)
    {
        Assert.Equal(SearchSortOrder.Relevance, SearchSortOrders.Parse(value));
    }

    [Fact]
    public void ToQueryValue_RoundTripsEveryOrder()
    {
        foreach (var order in Enum.GetValues<SearchSortOrder>())
        {
            Assert.Equal(order, SearchSortOrders.Parse(order.ToQueryValue()));
        }
    }

    [Fact]
    public void ToQueryValue_UnknownOrder_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((SearchSortOrder)100).ToQueryValue());
    }
}
