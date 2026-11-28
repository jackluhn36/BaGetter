using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace BaGest.Core;

/// <summary>
/// Converts between <see cref="SearchSortOrder"/> and the values of the <c>sortBy</c> search parameter.
/// </summary>
/// <remarks>
/// The values and their parsing match nuget.org's gallery search, see
/// <see href="https://github.com/NuGet/NuGetGallery/blob/main/src/NuGet.Services.AzureSearch/SearchService/Models/ParameterUtilities.cs"/>.
/// </remarks>
public static class SearchSortOrders
{
    private static readonly FrozenDictionary<string, SearchSortOrder> OrdersByValue =
        new Dictionary<string, SearchSortOrder>
        {
            { "relevance", SearchSortOrder.Relevance },
            { "lastEdited", SearchSortOrder.LastEdited },
            { "published", SearchSortOrder.Published },
            { "title-asc", SearchSortOrder.TitleAscending },
            { "title-desc", SearchSortOrder.TitleDescending },
            { "created-asc", SearchSortOrder.CreatedAscending },
            { "created-desc", SearchSortOrder.CreatedDescending },
            { "totalDownloads-asc", SearchSortOrder.TotalDownloadsAscending },
            { "totalDownloads-desc", SearchSortOrder.TotalDownloadsDescending },
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<SearchSortOrder, string> ValuesByOrder =
        OrdersByValue.ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// Parses a <c>sortBy</c> value, ignoring case. As on nuget.org, a missing or unknown value
    /// means <see cref="SearchSortOrder.Relevance"/>.
    /// </summary>
    public static SearchSortOrder Parse(string value)
    {
        return value != null && OrdersByValue.TryGetValue(value, out var order)
            ? order
            : SearchSortOrder.Relevance;
    }

    /// <summary>
    /// The <c>sortBy</c> value that requests <paramref name="order"/>.
    /// </summary>
    public static string ToQueryValue(this SearchSortOrder order)
    {
        return ValuesByOrder.TryGetValue(order, out var value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(order), order, "Unknown search sort order.");
    }
}
