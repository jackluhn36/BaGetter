using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGest.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace BaGest.Tests.Shared.Search;

/// <summary>
/// Runs <see cref="DatabaseSearchService"/> against a real database. Each database provider derives from this
/// class and supplies a throwaway database, so the same expectations hold on every supported engine.
/// </summary>
public abstract class DatabaseSearchServiceFacts<TContext> : IAsyncLifetime
    where TContext : DbContext, IContext
{
    private readonly ITestOutputHelper _output;

    protected DatabaseSearchServiceFacts(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Whether the database server is configured. The tests are skipped otherwise.
    /// </summary>
    protected abstract bool IsAvailable { get; }

    /// <summary>
    /// Creates the empty database the tests run against.
    /// </summary>
    protected abstract Task CreateDatabaseAsync();

    /// <summary>
    /// Drops the database created by <see cref="CreateDatabaseAsync"/>.
    /// </summary>
    protected abstract Task DropDatabaseAsync();

    /// <summary>
    /// Creates a new context connected to the test database. The context must configure its database provider
    /// itself, as it does in the application; <paramref name="options"/> only carries the test's logging.
    /// </summary>
    protected abstract TContext CreateContext(DbContextOptions<TContext> options);

    public async Task InitializeAsync()
    {
        if (!IsAvailable) return;

        await CreateDatabaseAsync();

        await using var context = CreateContext(new DbContextOptions<TContext>());
        await context.RunMigrationsAsync(CancellationToken.None);
        context.Packages.AddRange(SeedPackages());
        await context.SaveChangesAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable) return;

        await DropDatabaseAsync();
    }

    [SkippableTheory]
    [InlineData("acme.widgets")]
    [InlineData("ACME.WIDGETS")]
    [InlineData("Acme.Widgets")]
    public async Task Search_MatchesIdIgnoringCase(string query)
    {
        Assert.Equal(["Acme.Widgets"], await SearchAsync(query));
    }

    [SkippableTheory]
    [InlineData("structured")]
    [InlineData("STRUCTURED")]
    public async Task Search_MatchesDescriptionIgnoringCase(string query)
    {
        Assert.Equal(["Contoso.Logging"], await SearchAsync(query));
    }

    [SkippableTheory]
    [InlineData("pluggable")]
    [InlineData("PLUGGABLE")]
    public async Task Search_MatchesSummaryIgnoringCase(string query)
    {
        Assert.Equal(["Acme.Widgets"], await SearchAsync(query));
    }

    [SkippableTheory]
    [InlineData("analytics")]
    [InlineData("ANALYTICS")]
    public async Task Search_MatchesTitleIgnoringCase(string query)
    {
        Assert.Equal(["Contoso.Logging"], await SearchAsync(query));
    }

    [SkippableFact]
    public async Task Search_RequiresAllKeywords()
    {
        Assert.Equal(["Contoso.Logging"], await SearchAsync("CONTOSO structured"));
        Assert.Empty(await SearchAsync("contoso widgets"));
    }

    [SkippableFact]
    public async Task Search_KeywordsDifferingOnlyInCaseCountOnceTowardsTheLimit()
    {
        // Ten spellings of one keyword followed by a keyword no package matches together with it.
        // If the spellings were kept apart, the last keyword would be dropped by the limit.
        var query = "acme Acme ACME aCME acME acmE AcMe aCmE ACme AcME contoso";

        Assert.Empty(await SearchAsync(query));
    }

    [SkippableFact]
    public async Task Search_DoesNotDecodeTheQueryAgain()
    {
        // ASP.NET Core has already decoded the query string, so "%2E" here is literal text, not a dot.
        Assert.Empty(await SearchAsync("Acme%2EWidgets"));
    }

    [SkippableTheory]
    [InlineData("acme")]
    [InlineData("ACME")]
    public async Task Autocomplete_MatchesIdIgnoringCase(string query)
    {
        Assert.Equal(["Acme.Widgets"], await AutocompleteAsync(query));
    }

    [SkippableFact]
    public async Task Autocomplete_DoesNotMatchDescription()
    {
        Assert.Empty(await AutocompleteAsync("structured"));
    }

    [SkippableFact]
    public async Task Autocomplete_DoesNotDecodeTheQueryAgain()
    {
        Assert.Empty(await AutocompleteAsync("Acme%2EWidgets"));
    }

    [SkippableTheory]
    [InlineData(SearchSortOrder.LastEdited, new[] { "beta.tools", "Acme.Widgets", "Contoso.Logging", "Fabrikam.Reusable" })]
    [InlineData(SearchSortOrder.Published, new[] { "beta.tools", "Acme.Widgets", "Contoso.Logging", "Fabrikam.Reusable" })]
    [InlineData(SearchSortOrder.CreatedDescending, new[] { "beta.tools", "Acme.Widgets", "Contoso.Logging", "Fabrikam.Reusable" })]
    [InlineData(SearchSortOrder.CreatedAscending, new[] { "Fabrikam.Reusable", "Contoso.Logging", "Acme.Widgets", "beta.tools" })]
    [InlineData(SearchSortOrder.TitleAscending, new[] { "Acme.Widgets", "Contoso.Logging", "beta.tools", "Fabrikam.Reusable" })]
    [InlineData(SearchSortOrder.TitleDescending, new[] { "Fabrikam.Reusable", "beta.tools", "Contoso.Logging", "Acme.Widgets" })]
    [InlineData(SearchSortOrder.TotalDownloadsAscending, new[] { "beta.tools", "Fabrikam.Reusable", "Acme.Widgets", "Contoso.Logging" })]
    [InlineData(SearchSortOrder.TotalDownloadsDescending, new[] { "Contoso.Logging", "Acme.Widgets", "Fabrikam.Reusable", "beta.tools" })]
    public async Task Search_SortsPackages(SearchSortOrder sortOrder, string[] expected)
    {
        // The titles test that the newest version's title is used ("Zeta Widgets" is older),
        // and that the ID is used when the title is missing or empty.
        Assert.Equal(expected, await SearchAsync(query: null, sortOrder));
    }

    [SkippableFact]
    public async Task Search_SortsByAllVersionsOfAMatchingPackage()
    {
        // Only Acme.Widgets 1.0.0 (10 downloads) mentions "reusable", but the package has 15 downloads in total.
        Assert.Equal(
            ["Acme.Widgets", "Fabrikam.Reusable"],
            await SearchAsync("reusable", SearchSortOrder.TotalDownloadsDescending));
    }

    [SkippableFact]
    public async Task Search_PagesSortedPackages()
    {
        Assert.Equal(
            ["Acme.Widgets", "Fabrikam.Reusable"],
            await SearchAsync(query: null, SearchSortOrder.TotalDownloadsDescending, skip: 1, take: 2));
    }

    [SkippableFact]
    public async Task Search_SortedResultsIncludeAllVersions()
    {
        Skip.IfNot(IsAvailable);

        await using var context = CreateQueryContext();
        var response = await CreateService(context).SearchAsync(
            new SearchRequest
            {
                Query = "acme",
                Take = 20,
                IncludePrerelease = true,
                IncludeSemVer2 = true,
                SortOrder = SearchSortOrder.TotalDownloadsDescending,
            },
            CancellationToken.None);

        var result = Assert.Single(response.Data);
        Assert.Equal("2.0.0", result.Version);
        Assert.Equal(15, result.TotalDownloads);
        Assert.Equal(["2.0.0", "1.0.0"], result.Versions.Select(version => version.Version));
    }

    private async Task<IReadOnlyList<string>> SearchAsync(
        string query,
        SearchSortOrder sortOrder = SearchSortOrder.Relevance,
        int skip = 0,
        int take = 20)
    {
        Skip.IfNot(IsAvailable);

        await using var context = CreateQueryContext();
        var response = await CreateService(context).SearchAsync(
            new SearchRequest
            {
                Query = query,
                Skip = skip,
                Take = take,
                IncludePrerelease = true,
                IncludeSemVer2 = true,
                SortOrder = sortOrder,
            },
            CancellationToken.None);

        return response.Data.Select(result => result.PackageId).ToList();
    }

    private async Task<IReadOnlyList<string>> AutocompleteAsync(string query)
    {
        Skip.IfNot(IsAvailable);

        await using var context = CreateQueryContext();
        var response = await CreateService(context).AutocompleteAsync(
            new AutocompleteRequest
            {
                Query = query,
                Take = 20,
                IncludePrerelease = true,
                IncludeSemVer2 = true,
            },
            CancellationToken.None);

        return response.Data;
    }

    /// <summary>
    /// Creates a context that writes the SQL it executes to the test output.
    /// </summary>
    private TContext CreateQueryContext() => CreateContext(
        new DbContextOptionsBuilder<TContext>()
            .LogTo(_output.WriteLine, [RelationalEventId.CommandExecuted], LogLevel.Information)
            .Options);

    private static DatabaseSearchService CreateService(IContext context) => new(
        context,
        Mock.Of<IFrameworkCompatibilityService>(),
        new SearchResponseBuilder(Mock.Of<IUrlGenerator>()));

    private static IEnumerable<Package> SeedPackages()
    {
        yield return CreatePackage(
            "Acme.Widgets", "1.0.0", downloads: 10, published: new DateTime(2026, 1, 1),
            title: "Zeta Widgets", description: "Reusable UI widgets");
        yield return CreatePackage(
            "Acme.Widgets", "2.0.0", downloads: 5, published: new DateTime(2026, 3, 1),
            title: "Acme Widgets", description: "Widgets for dashboards", summary: "Pluggable components");
        yield return CreatePackage(
            "beta.tools", "0.1.0", downloads: 1, published: new DateTime(2026, 4, 1),
            title: "", description: "Command line helpers");
        yield return CreatePackage(
            "Contoso.Logging", "1.0.0", downloads: 100, published: new DateTime(2026, 2, 1),
            title: "Analytics Logger", description: "Structured LOGGING library");
        yield return CreatePackage(
            "Fabrikam.Reusable", "1.0.0", downloads: 12, published: new DateTime(2026, 1, 15),
            title: null, description: "Shared helpers");
    }

    private static Package CreatePackage(
        string id,
        string version,
        long downloads,
        DateTime published,
        string title,
        string description,
        string summary = null) => new()
    {
        Id = id,
        Version = NuGetVersion.Parse(version),
        Authors = ["Test author"],
        Tags = [],
        Title = title,
        Description = description,
        Summary = summary,
        Downloads = downloads,
        Published = DateTime.SpecifyKind(published, DateTimeKind.Utc),
        Listed = true,
        Dependencies = [],
        PackageTypes = [],
        TargetFrameworks = [],
    };
}
