using System;
using System.Collections.Generic;
using System.Diagnostics;
using NuGet.Versioning;

namespace BaGest.Core;

// See NuGetGallery's: https://github.com/NuGet/NuGetGallery/blob/master/src/NuGetGallery.Core/Entities/Package.cs
[DebuggerDisplay("{Id} {Version}")]
public class Package
{
    public int Key { get; set; }

    public string Id { get; set; } = string.Empty;

    public NuGetVersion Version
    {
        get
        {
            // Favor the original version string as it contains more information.
            // Packages uploaded with older versions of BaGest may not have the original version string.
            return NuGetVersion.Parse(
                OriginalVersionString ?? NormalizedVersionString);
        }

        set
        {
            NormalizedVersionString = value.ToNormalizedString().ToLowerInvariant();
            OriginalVersionString = value.OriginalVersion;
        }
    }

    public string[] Authors { get; set; }
    public string Description { get; set; }
    public long Downloads { get; set; }
    public bool HasReadme { get; set; }
    public bool HasEmbeddedIcon { get; set; }
    public bool IsPrerelease { get; set; }
    public string? CachedFrom { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? Language { get; set; }
    public bool Listed { get; set; }
    public string? MinClientVersion { get; set; }
    public DateTime Published { get; set; }
    public bool RequireLicenseAcceptance { get; set; }
    public SemVerLevel SemVerLevel { get; set; }
    public string Summary { get; set; }
    public string Title { get; set; }

    public Uri? IconUrl { get; set; }
    public Uri? LicenseUrl { get; set; }
    public Uri? ProjectUrl { get; set; }

    public Uri? RepositoryUrl { get; set; }
    public string? RepositoryType { get; set; }

    public string[]? Tags { get; set; }

    /// <summary>
    /// Why this package version is deprecated, or <see cref="PackageDeprecationReasons.None"/>.
    /// </summary>
    public PackageDeprecationReasons DeprecationReasons { get; set; }

    /// <summary>
    /// Additional details about the deprecation.
    /// </summary>
    public string DeprecationMessage { get; set; }

    /// <summary>
    /// The id of the package that should be used instead of this deprecated package.
    /// </summary>
    public string DeprecationAlternatePackageId { get; set; }

    /// <summary>
    /// The version range of the alternate package, or null if any version is allowed.
    /// </summary>
    public string DeprecationAlternatePackageVersionRange { get; set; }

    /// <summary>
    /// Used for optimistic concurrency.
    /// </summary>
    public byte[] RowVersion { get; set; }

    public List<PackageDependency> Dependencies { get; set; }
    public List<PackageType> PackageTypes { get; set; }
    public List<TargetFramework> TargetFrameworks { get; set; }

    public string NormalizedVersionString { get; set; }
    public string OriginalVersionString { get; set; }


    public string IconUrlString => IconUrl?.AbsoluteUri ?? string.Empty;
    public string LicenseUrlString => LicenseUrl?.AbsoluteUri ?? string.Empty;
    public string ProjectUrlString => ProjectUrl?.AbsoluteUri ?? string.Empty;
    public string RepositoryUrlString => RepositoryUrl?.AbsoluteUri ?? string.Empty;

    /// <summary>
    /// Whether this package version is deprecated.
    /// </summary>
    public bool IsDeprecated => DeprecationReasons != PackageDeprecationReasons.None;

    /// <summary>
    /// Mark this package version as deprecated, replacing any previous deprecation.
    /// </summary>
    /// <param name="deprecation">The deprecation to apply.</param>
    public void Deprecate(PackageDeprecationDetails deprecation)
    {
        ArgumentNullException.ThrowIfNull(deprecation);

        DeprecationReasons = deprecation.Reasons;
        DeprecationMessage = deprecation.Message;
        DeprecationAlternatePackageId = deprecation.AlternatePackageId;
        DeprecationAlternatePackageVersionRange = deprecation.AlternatePackageVersionRange;
    }

    /// <summary>
    /// Remove the deprecation of this package version.
    /// </summary>
    public void Undeprecate()
    {
        DeprecationReasons = PackageDeprecationReasons.None;
        DeprecationMessage = null;
        DeprecationAlternatePackageId = null;
        DeprecationAlternatePackageVersionRange = null;
    }
}
