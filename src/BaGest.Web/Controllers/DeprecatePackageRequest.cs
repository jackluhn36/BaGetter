using System;
using System.Collections.Generic;

namespace BaGest.Web;

/// <summary>
/// The body of a request to deprecate a package version.
/// </summary>
public class DeprecatePackageRequest
{
    /// <summary>
    /// The reasons why the package is deprecated: "Legacy", "CriticalBugs" and/or "Other".
    /// </summary>
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional additional details about the deprecation.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Optional id of the package that should be used instead.
    /// </summary>
    public string? AlternatePackageId { get; set; }

    /// <summary>
    /// Optional version range of the alternate package. Omit it or use "*" to allow any version.
    /// </summary>
    public string? AlternatePackageVersionRange { get; set; }
}
