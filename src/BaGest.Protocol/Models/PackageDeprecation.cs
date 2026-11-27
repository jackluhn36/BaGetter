using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using BaGest.Protocol.Internal;

namespace BaGest.Protocol.Models;

/// <summary>
/// A package's metadata.
/// </summary>
/// <remarks>See: <see href="https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#package-deprecation"/></remarks>
public class PackageDeprecation
{
    /// <summary>
    /// The URL to the document used to produce this object.
    /// </summary>
    [JsonPropertyName("@id")]
    public string CatalogLeafUrl { get; set; } = string.Empty;

    /// <summary>
    /// The reasons why the package was deprecated.
    /// Deprecation reasons include: "Legacy", "CriticalBugs", and "Other".
    /// </summary>
    [JsonPropertyName("reasons")]
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The additional details about this deprecation.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// The alternate package that should be used instead.
    /// </summary>
    [JsonPropertyName("alternatePackage")]
    public AlternatePackage? AlternatePackage { get; set; }
}
