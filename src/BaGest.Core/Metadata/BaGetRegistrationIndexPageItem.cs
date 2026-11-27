using System.Text.Json.Serialization;
using BaGest.Protocol.Models;

namespace BaGest.Core;

/// <summary>
/// BaGest's extensions to a registration index page.
/// </summary>
/// <remarks>Extends <see cref="RegistrationIndexPageItem"/>.</remarks>
public class BaGetRegistrationIndexPageItem : RegistrationIndexPageItem
{
    /// <summary>
    /// The catalog entry containing the package metadata.
    /// </summary>
    /// <remarks>This was modified to use BaGest's extended package metadata model.</remarks>
    [JsonPropertyName("catalogEntry")]
    [JsonPropertyOrder(int.MaxValue)]
    public new BaGestPackageMetadata? PackageMetadata { get; set; }
}
