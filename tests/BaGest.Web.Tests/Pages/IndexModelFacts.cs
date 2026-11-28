using System.Threading;
using System.Threading.Tasks;
using BaGest.Core;
using BaGest.Protocol.Models;
using Moq;
using Xunit;

namespace BaGest.Web.Tests;

public class IndexModelFacts
{
    private readonly IndexModel _target;

    private SearchRequest _capturedRequest = null;
    private readonly SearchResponse _response = new SearchResponse();
    private readonly CancellationToken _cancellation = CancellationToken.None;

    public IndexModelFacts()
    {
        var search = new Mock<ISearchService>();
        search
            .Setup(s => s.SearchAsync(It.IsAny<SearchRequest>(), _cancellation))
            .Callback((SearchRequest r, CancellationToken c) => _capturedRequest = r)
            .ReturnsAsync(_response);

        _target = new IndexModel(search.Object);
    }

    [Fact]
    public async Task DefaultSearch()
    {
        await _target.OnGetAsync(_cancellation);

        Assert.Equal(0, _capturedRequest.Skip);
        Assert.Equal(20, _capturedRequest.Take);
        Assert.True(_capturedRequest.IncludePrerelease);
        Assert.True(_capturedRequest.IncludeSemVer2);
        Assert.Null(_capturedRequest.PackageType);
        Assert.Null(_capturedRequest.Framework);
        Assert.Null(_capturedRequest.Query);
        Assert.Equal(SearchSortOrder.Relevance, _capturedRequest.SortOrder);
    }

    [Fact]
    public async Task AcceptsParameters()
    {
        _target.Prerelease = false;
        _target.PageIndex = 5;
        _target.PackageType = "foo";
        _target.Framework = "bar";
        _target.Query = "Hello world";

        await _target.OnGetAsync(_cancellation);

        Assert.Equal(80, _capturedRequest.Skip);
        Assert.Equal(20, _capturedRequest.Take);
        Assert.False(_capturedRequest.IncludePrerelease);
        Assert.True(_capturedRequest.IncludeSemVer2);
        Assert.Equal("foo", _capturedRequest.PackageType);
        Assert.Equal("bar", _capturedRequest.Framework);
        Assert.Equal("Hello world", _capturedRequest.Query);
    }

    [Theory]
    [InlineData("totalDownloads-desc", SearchSortOrder.TotalDownloadsDescending)]
    [InlineData("CREATED-DESC", SearchSortOrder.CreatedDescending)]
    [InlineData("unknown", SearchSortOrder.Relevance)]
    public async Task PassesSortOrder(string sortBy, SearchSortOrder expected)
    {
        _target.SortBy = sortBy;

        await _target.OnGetAsync(_cancellation);

        Assert.Equal(expected, _capturedRequest.SortOrder);
    }
}
