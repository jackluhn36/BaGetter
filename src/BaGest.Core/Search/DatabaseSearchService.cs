using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGest.Protocol.Models;
using Microsoft.EntityFrameworkCore;

namespace BaGest.Core;

public class DatabaseSearchService : ISearchService
{
    private readonly IContext _context;
    private readonly IFrameworkCompatibilityService _frameworks;
    private readonly ISearchResponseBuilder _searchBuilder;

    public DatabaseSearchService(IContext context, IFrameworkCompatibilityService frameworks, ISearchResponseBuilder searchBuilder)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(frameworks);
        ArgumentNullException.ThrowIfNull(searchBuilder);

        _context = context;
        _frameworks = frameworks;
        _searchBuilder = searchBuilder;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var frameworks = GetCompatibleFrameworksOrNull(request.Framework);

        IQueryable<Package> search = _context.Packages;
        search = ApplySearchQuery(search, request.Query);
        search = ApplySearchFilters(
            search,
            request.IncludePrerelease,
            request.IncludeSemVer2,
            request.PackageType,
            frameworks);

        var packages = request.SortOrder == SearchSortOrder.Relevance
            ? await FindPackagesOrderedByIdAsync(search, request, frameworks, cancellationToken)
            : await FindSortedPackagesAsync(search, request, frameworks, cancellationToken);

        return _searchBuilder.BuildSearch(packages);
    }

    private async Task<IReadOnlyList<PackageRegistration>> FindPackagesOrderedByIdAsync(
        IQueryable<Package> search,
        SearchRequest request,
        IReadOnlyList<string> frameworks,
        CancellationToken cancellationToken)
    {
        var packageIds = search
            .Select(p => p.Id)
            .Distinct()
            .OrderBy(id => id)
            .Skip(request.Skip)
            .Take(request.Take);

        // This query MUST fetch all versions for each package that matches the search,
        // otherwise the results for a package's latest version may be incorrect.
        // If possible, we'll find all these packages in a single query by matching
        // the package IDs in a subquery. Otherwise, run two queries:
        //   1. Find the package IDs that match the search
        //   2. Find all package versions for these package IDs
        if (_context.SupportsLimitInSubqueries)
        {
            search = _context.Packages.Where(p => packageIds.Contains(p.Id));
        }
        else
        {
            var packageIdResults = await packageIds.ToListAsync(cancellationToken);

            search = _context.Packages.Where(p => packageIdResults.Contains(p.Id));
        }

        search = ApplySearchFilters(
            search,
            request.IncludePrerelease,
            request.IncludeSemVer2,
            request.PackageType,
            frameworks);

        var results = await search.ToListAsync(cancellationToken);
        return results
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PackageRegistration(group.Key, group.ToList()))
            .ToList();
    }

    private async Task<IReadOnlyList<PackageRegistration>> FindSortedPackagesAsync(
        IQueryable<Package> search,
        SearchRequest request,
        IReadOnlyList<string> frameworks,
        CancellationToken cancellationToken)
    {
        // The sort keys are computed from all the versions returned for a package, not only from the
        // versions that matched the query, so that the order agrees with the response (its total
        // downloads, for example).
        var versions = ApplySearchFilters(
            _context.Packages,
            request.IncludePrerelease,
            request.IncludeSemVer2,
            request.PackageType,
            frameworks);
        var matchingIds = search.Select(p => p.Id);

        var registrations = versions
            .Where(p => matchingIds.Contains(p.Id))
            .GroupBy(p => p.Id)
            .Select(group => new
            {
                Id = group.Key,
                TotalDownloads = group.Sum(p => p.Downloads),
                LastPublished = group.Max(p => p.Published),

                // Like nuget.org's sortable title: the title of the most recently published version,
                // or the ID if it has none, ignoring case.
                SortableTitle = group
                    .OrderByDescending(p => p.Published)
                    .ThenByDescending(p => p.Key)
                    .Select(p => (p.Title == null || p.Title.Trim() == "" ? p.Id : p.Title.Trim()).ToLower())
                    .FirstOrDefault(),
            });

        var sorted = request.SortOrder switch
        {
            SearchSortOrder.LastEdited
                or SearchSortOrder.Published
                or SearchSortOrder.CreatedDescending => registrations.OrderByDescending(r => r.LastPublished),
            SearchSortOrder.CreatedAscending => registrations.OrderBy(r => r.LastPublished),
            SearchSortOrder.TitleAscending => registrations.OrderBy(r => r.SortableTitle),
            SearchSortOrder.TitleDescending => registrations.OrderByDescending(r => r.SortableTitle),
            SearchSortOrder.TotalDownloadsAscending => registrations.OrderBy(r => r.TotalDownloads),
            SearchSortOrder.TotalDownloadsDescending => registrations.OrderByDescending(r => r.TotalDownloads),
            _ => throw new ArgumentOutOfRangeException(
                $"{nameof(request)}.{nameof(request.SortOrder)}", request.SortOrder, "Unknown search sort order."),
        };

        // The ID breaks ties, so that pages do not overlap.
        var packageIds = await sorted
            .ThenBy(r => r.Id)
            .Select(r => r.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync(cancellationToken);

        var results = await versions
            .Where(p => packageIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // With a case-sensitive collation, the database can return IDs that differ only in case.
        // They are one package here, as in the rest of BaGest, so the first position wins.
        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var packageId in packageIds)
        {
            positions.TryAdd(packageId, positions.Count);
        }

        return results
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => positions[group.Key])
            .Select(group => new PackageRegistration(group.Key, group.ToList()))
            .ToList();
    }

    public async Task<AutocompleteResponse> AutocompleteAsync(AutocompleteRequest request, CancellationToken cancellationToken)
    {
        IQueryable<Package> search = _context.Packages;

        search = ApplyAutocompleteQuery(search, request.Query);
        search = ApplySearchFilters(
            search,
            request.IncludePrerelease,
            request.IncludeSemVer2,
            request.PackageType,
            frameworks: null);

        var packageIds = await search
            .OrderByDescending(p => p.Downloads)
            .Select(p => p.Id)
            .Distinct()
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync(cancellationToken);

        return _searchBuilder.BuildAutocomplete(packageIds);
    }

    public async Task<AutocompleteResponse> ListPackageVersionsAsync(VersionsRequest request, CancellationToken cancellationToken)
    {
        var packageId = request.PackageId.ToLowerInvariant();
        var search = _context
            .Packages
            .Where(p => p.Id.ToLower().Equals(packageId));

        search = ApplySearchFilters(
            search,
            request.IncludePrerelease,
            request.IncludeSemVer2,
            packageType: null,
            frameworks: null);

        var packageVersions = await search
            .Select(p => p.NormalizedVersionString)
            .ToListAsync(cancellationToken);

        return _searchBuilder.BuildAutocomplete(packageVersions);
    }

    public async Task<DependentsResponse> FindDependentsAsync(string packageId, CancellationToken cancellationToken)
    {
        var dependents = await _context
            .Packages
            .Where(p => p.Listed)
            .OrderByDescending(p => p.Downloads)
            .Where(p => p.Dependencies.Any(d => d.Id == packageId))
            .Take(20)
            .Select(r => new PackageDependent
            {
                Id = r.Id,
                Description = r.Description,
                TotalDownloads = r.Downloads
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        return _searchBuilder.BuildDependents(dependents);
    }
    
    /// <summary>
    /// The maximum number of keywords applied to a query. Each keyword adds a condition to the SQL statement,
    /// so an unbounded number could exceed the database's expression limits.
    /// </summary>
    private const int MaxKeywords = 10;

    [SuppressMessage("Performance", "CA1862", Justification = "EF Core translates ToLower(), not the StringComparison overloads.")]
    private static IQueryable<Package> ApplySearchQuery(IQueryable<Package> query, string search)
    {
        foreach (var term in ParseKeywords(search))
        {
            // Search across multiple fields for the main search endpoint.
            // Lowercase the columns too: not every provider compares them case-insensitively
            // (SQLite's instr() ignores the column collation, PostgreSQL stores them as plain text).
            // The pattern has a leading wildcard, so no index could be used anyway.
            query = query.Where(p =>
                p.Id.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)) ||
                (p.Summary != null && p.Summary.ToLower().Contains(term)) ||
                (p.Title != null && p.Title.ToLower().Contains(term))
            );
        }

        return query;
    }

    [SuppressMessage("Performance", "CA1862", Justification = "EF Core translates ToLower(), not the StringComparison overloads.")]
    private static IQueryable<Package> ApplyAutocompleteQuery(IQueryable<Package> query, string search)
    {
        foreach (var term in ParseKeywords(search))
        {
            // Autocomplete should ONLY match the package ID
            query = query.Where(p => p.Id.ToLower().Contains(term));
        }

        return query;
    }

    /// <summary>
    /// Splits a query into the lowercase keywords that must all match. The query is used as received:
    /// ASP.NET Core has already decoded the query string, and decoding it again would change literal
    /// percent signs.
    /// </summary>
    private static IEnumerable<string> ParseKeywords(string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return [];
        }

        return search
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(keyword => keyword.ToLowerInvariant())
            .Distinct()
            .Take(MaxKeywords);
    }

    private static IQueryable<Package> ApplySearchFilters(
        IQueryable<Package> query,
        bool includePrerelease,
        bool includeSemVer2,
        string? packageType,
        IReadOnlyList<string>? frameworks)
    {
        if (!includePrerelease)
        {
            query = query.Where(p => !p.IsPrerelease);
        }

        if (!includeSemVer2)
        {
            query = query.Where(p => p.SemVerLevel != SemVerLevel.SemVer2);
        }

        if (!string.IsNullOrEmpty(packageType))
        {
            query = query.Where(p => p.PackageTypes.Any(t => t.Name == packageType));
        }

        if (frameworks != null)
        {
            query = query.Where(p => p.TargetFrameworks.Any(f => frameworks.Contains(f.Moniker)));
        }

        return query.Where(p => p.Listed);
    }

    private IReadOnlyList<string> GetCompatibleFrameworksOrNull(string framework)
    {
        if (framework == null) return Array.Empty<string>();

        return _frameworks.FindAllCompatibleFrameworks(framework);
    }
}
