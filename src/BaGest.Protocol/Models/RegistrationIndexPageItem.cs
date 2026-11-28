using System.Text.Json.Serialization;

namespace BaGest.Protocol.Models;

/// <summary>
/// An item in the <see cref="CatalogIndex"/> that references a <see cref="CatalogLeaf"/>.
/// </summary>
/// <remarks>See <see href="https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-leaf-object-in-a-page"/></remarks>
public class RegistrationIndexPageItem
{
    /// <summary>
    /// The URL to the registration leaf.
    /// </summary>
    [JsonPropertyName("@id")]
    public string RegistrationLeafUrl { get; set; } = string.Empty;

    /// <summary>
    /// The catalog entry containing the package metadata.
    /// </summary>
    [JsonPropertyName("catalogEntry")]
    public PackageMetadata PackageMetadata { get; set; } = default!;

    /// <summary>
    /// The URL to the package content (.nupkg)
    /// </summary>
    [JsonPropertyName("packageContent")]
    public string PackageContentUrl { get; set; } = string.Empty;
}
