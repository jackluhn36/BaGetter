using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Versioning;

namespace BaGest.Core;

/// <summary>
/// A validated description of why a package version is deprecated.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/nuget/api/registration-base-url-resource#package-deprecation"/></remarks>
public sealed record PackageDeprecationDetails
{
    /// <summary>
    /// The value of the NuGet protocol meaning any version of the alternate package is allowed.
    /// </summary>
    public const string AnyVersionRange = "*";

    /// <summary>
    /// The maximum length of <see cref="Message"/>.
    /// </summary>
    public const int MaxMessageLength = 4000;

    /// <summary>
    /// The maximum length of <see cref="AlternatePackageId"/>, the same as the maximum length of a package id.
    /// </summary>
    public const int MaxAlternatePackageIdLength = 128;

    /// <summary>
    /// The maximum length of <see cref="AlternatePackageVersionRange"/>.
    /// </summary>
    public const int MaxAlternatePackageVersionRangeLength = 256;

    private PackageDeprecationDetails(
        PackageDeprecationReasons reasons,
        string? message,
        string? alternatePackageId,
        string? alternatePackageVersionRange)
    {
        Reasons = reasons;
        Message = message;
        AlternatePackageId = alternatePackageId;
        AlternatePackageVersionRange = alternatePackageVersionRange;
    }

    /// <summary>
    /// The reasons why the package was deprecated. Never <see cref="PackageDeprecationReasons.None"/>.
    /// </summary>
    public PackageDeprecationReasons Reasons { get; }

    /// <summary>
    /// Additional details about the deprecation, or null.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// The id of the package that should be used instead, or null.
    /// </summary>
    public string? AlternatePackageId { get; }

    /// <summary>
    /// The normalized version range of the alternate package, or null if any version is allowed.
    /// </summary>
    public string? AlternatePackageVersionRange { get; }

    /// <summary>
    /// Validate the raw input of a deprecation and create its details.
    /// </summary>
    /// <param name="reasons">The NuGet protocol reasons: "Legacy", "CriticalBugs" or "Other" (case-insensitive).</param>
    /// <param name="message">Optional additional details.</param>
    /// <param name="alternatePackageId">Optional id of the package to use instead.</param>
    /// <param name="alternatePackageVersionRange">
    /// Optional version range of the alternate package. Null, empty or "*" allows any version.
    /// </param>
    /// <param name="details">The created details, or null if the input is invalid.</param>
    /// <param name="error">Why the input is invalid, or null.</param>
    /// <returns>Whether the input is valid.</returns>
    public static bool TryCreate(
        IEnumerable<string> reasons,
        string? message,
        string? alternatePackageId,
        string? alternatePackageVersionRange,
        out PackageDeprecationDetails? details,
        out string? error)
    {
        details = null;

        if (!TryParseReasons(reasons, out var parsedReasons, out error))
        {
            return false;
        }

        message = NullIfWhiteSpace(message);
        if (message?.Length > MaxMessageLength)
        {
            error = $"The message must not be longer than {MaxMessageLength} characters.";
            return false;
        }

        alternatePackageId = NullIfWhiteSpace(alternatePackageId);
        alternatePackageVersionRange = NullIfWhiteSpace(alternatePackageVersionRange);

        if (alternatePackageId == null)
        {
            if (alternatePackageVersionRange != null)
            {
                error = "An alternate package version range requires an alternate package id.";
                return false;
            }
        }
        else
        {
            if (alternatePackageId.Length > MaxAlternatePackageIdLength
                || !PackageIdValidator.IsValidPackageId(alternatePackageId))
            {
                error = $"'{alternatePackageId}' is not a valid package id.";
                return false;
            }

            if (!TryNormalizeVersionRange(alternatePackageVersionRange, out alternatePackageVersionRange))
            {
                error = "The alternate package version range is not a valid NuGet version range.";
                return false;
            }
        }

        details = new PackageDeprecationDetails(parsedReasons, message, alternatePackageId, alternatePackageVersionRange);
        error = null;
        return true;
    }

    private static bool TryParseReasons(IEnumerable<string> values, out PackageDeprecationReasons reasons, out string error)
    {
        reasons = PackageDeprecationReasons.None;

        foreach (var value in values ?? [])
        {
            if (!PackageDeprecationReasonsExtensions.TryParseProtocolValue(value, out var reason))
            {
                error = $"Unknown deprecation reason '{value}'. Allowed reasons: {PackageDeprecationReasonsExtensions.AllowedProtocolValues}.";
                return false;
            }

            reasons |= reason;
        }

        if (reasons == PackageDeprecationReasons.None)
        {
            error = $"At least one deprecation reason is required. Allowed reasons: {PackageDeprecationReasonsExtensions.AllowedProtocolValues}.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryNormalizeVersionRange(string? input, out string? normalized)
    {
        normalized = null;

        if (input == null || input == AnyVersionRange)
        {
            return true;
        }

        if (!VersionRange.TryParse(input, allowFloating: false, out var range) || !IsSatisfiable(range))
        {
            return false;
        }

        // A range without bounds, such as "(, )", allows any version too.
        if (!range.HasLowerBound && !range.HasUpperBound)
        {
            return true;
        }

        var normalizedRange = range.ToNormalizedString();
        if (normalizedRange.Length > MaxAlternatePackageVersionRangeLength)
        {
            return false;
        }

        normalized = normalizedRange;
        return true;
    }

    private static bool IsSatisfiable(VersionRange range)
    {
        if (!range.HasLowerAndUpperBounds)
        {
            return true;
        }

        var comparison = range.MinVersion.CompareTo(range.MaxVersion);

        return comparison < 0 || (comparison == 0 && range.IsMinInclusive && range.IsMaxInclusive);
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
