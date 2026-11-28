using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGest.Core;
using BaGest.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGest.Web;

public class IndexModel : PageModel
{
    private readonly ISearchService _search;

    public IndexModel(ISearchService? search)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
    }

    public const int ResultsPerPage = 20;

    [BindProperty(Name = "q", SupportsGet = true)]
    public string Query { get; set; } = string.Empty;

    [BindProperty(Name = "p", SupportsGet = true)]
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string PackageType { get; set; } = "any";

    [BindProperty(SupportsGet = true)]
    public string Framework { get; set; } = "any";

    [BindProperty(SupportsGet = true)]
    public bool Prerelease { get; set; } = true;

    /// <summary>
    /// The order of the results, as a <c>sortBy</c> value of the search API. See <see cref="SearchSortOrders"/>.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = string.Empty;// TODO

    public SearchSortOrder SortOrder => SearchSortOrders.Parse(SortBy);

    public IReadOnlyList<SearchResult> Packages { get; private set; } = Array.Empty<SearchResult>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();

        var packageType = PackageType == "any" ? null : PackageType;
        var framework = Framework == "any" ? null : Framework;

        var search = await _search.SearchAsync(
            new SearchRequest
            {
                Skip = (PageIndex - 1) * ResultsPerPage,
                Take = ResultsPerPage,
                IncludePrerelease = Prerelease,
                IncludeSemVer2 = true,
                PackageType = packageType,
                Framework = framework,
                Query = Query,
                SortOrder = SortOrder,
            },
            cancellationToken);

        Packages = search.Data;

        return Page();
    }
}
