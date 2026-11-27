using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using BaGest.Protocol.Models;

namespace BaGest.Core;

/// <summary>
/// BaGest's extensions to the package metadata model.
/// </summary>
/// <remarks>
/// Extends <see cref="PackageMetadata"/>.<br/>
/// These additions are not part of the official protocol.
/// </remarks>
public class BaGestPackageMetadata : PackageMetadata
{
    [JsonPropertyName("downloads")]
    public long Downloads { get; set; }

    [JsonPropertyName("hasReadme")]
    public bool HasReadme { get; set; }

    [JsonPropertyName("packageTypes")]
    public IReadOnlyList<string> PackageTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The package's release notes.
    /// </summary>
    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    [JsonPropertyName("repositoryType")]
    public string? RepositoryType { get; set; }
}
