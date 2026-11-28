using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BaGest.Protocol.Models;

/// <summary>
/// The metadata for a package and all of its versions.
/// </summary>
/// <remarks>See: <see href="https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-index"/></remarks>
public class RegistrationIndexResponse
{
    public static readonly IReadOnlyList<string> DefaultType = new List<string>
    {
        "catalog:CatalogRoot",
        "PackageRegistration",
        "catalog:Permalink"
    };

    /// <summary>
    /// The URL to the registration index.
    /// </summary>
    [JsonPropertyName("@id")]
    public string RegistrationIndexUrl { get; set; } = string.Empty;

    /// <summary>
    /// The registration index's type.
    /// </summary>
    [JsonPropertyName("@type")]
    public IReadOnlyList<string> Type { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The number of registration pages. See <see cref="Pages"/>.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>
    /// The pages that contain all of the versions of the package, ordered
    /// by the package's version.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<RegistrationIndexPage> Pages { get; set; } = Array.Empty<RegistrationIndexPage>();
}
