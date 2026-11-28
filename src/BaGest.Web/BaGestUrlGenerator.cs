using System;
using BaGest.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NuGet.Versioning;

namespace BaGest.Web;

// TODO: This should validate the "Host" header against known valid values
public class BaGestUrlGenerator : IUrlGenerator
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public BaGestUrlGenerator(IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _linkGenerator = linkGenerator ?? throw new ArgumentNullException(nameof(linkGenerator));
    }

    private HttpContext HttpContext => _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("An HTTP context is required to generate URLs.");

    public string GetServiceIndexUrl()
    {
        return GetUriByRouteValues(Routes.IndexRouteName, values: null);
    }

    public string GetPackageContentResourceUrl()
    {
        return AbsoluteUrl("v3/package/");
    }

    public string GetPackageMetadataResourceUrl()
    {
        return AbsoluteUrl("v3/registration/");
    }

    public string GetPackagePublishResourceUrl()
    {
        return GetUriByRouteValues(Routes.UploadPackageRouteName, values: null);
    }

    public string GetSymbolPublishResourceUrl()
    {
        return GetUriByRouteValues(Routes.UploadSymbolRouteName, values: null);
    }

    public string GetSearchResourceUrl()
    {
        return GetUriByRouteValues(Routes.SearchRouteName, values: null);
    }

    public string GetAutocompleteResourceUrl()
    {
        return GetUriByRouteValues(Routes.AutocompleteRouteName, values: null);
    }

    public string GetRegistrationIndexUrl(string id)
    {
        return GetUriByRouteValues(
            Routes.RegistrationIndexRouteName,
            values: new { Id = id.ToLowerInvariant() });
    }

    public string GetRegistrationPageUrl(string id, NuGetVersion lower, NuGetVersion upper)
    {
        // BaGest does not support paging the registration resource.
        throw new NotImplementedException();
    }

    public string GetRegistrationLeafUrl(string id, NuGetVersion version)
    {
        return GetUriByRouteValues(
            Routes.RegistrationLeafRouteName,
            values: new
            {
                Id = id.ToLowerInvariant(),
                Version = version.ToNormalizedString().ToLowerInvariant(),
            });
    }

    public string GetPackageVersionsUrl(string id)
    {
        return GetUriByRouteValues(
            Routes.PackageVersionsRouteName,
            values: new { Id = id.ToLowerInvariant() });
    }

    public string GetPackageDownloadUrl(string id, NuGetVersion version)
    {
        id = id.ToLowerInvariant();
        var versionString = version.ToNormalizedString().ToLowerInvariant();

        return GetUriByRouteValues(
            Routes.PackageDownloadRouteName,
            values: new
            {
                Id = id,
                Version = versionString,
                IdVersion = $"{id}.{versionString}"
            });
    }

    public string GetPackageManifestDownloadUrl(string id, NuGetVersion version)
    {
        id = id.ToLowerInvariant();
        var versionString = version.ToNormalizedString().ToLowerInvariant();

        return GetUriByRouteValues(
            Routes.PackageDownloadRouteName,
            values: new
            {
                Id = id,
                Version = versionString,
                Id2 = id,
            });
    }

    public string GetPackageIconDownloadUrl(string id, NuGetVersion version)
    {
        id = id.ToLowerInvariant();
        var versionString = version.ToNormalizedString().ToLowerInvariant();

        return GetUriByRouteValues(
            Routes.PackageDownloadIconRouteName,
            values: new
            {
                Id = id,
                Version = versionString
            });
    }

    private string GetUriByRouteValues(string routeName, object? values)
    {
        return _linkGenerator.GetUriByRouteValues(HttpContext, routeName, values)
            ?? throw new InvalidOperationException($"Could not generate a URL for route '{routeName}'.");
    }

    private string AbsoluteUrl(string relativePath)
    {
        var request = HttpContext.Request;

        return string.Concat(
            request.Scheme,
            "://",
            request.Host.ToUriComponent(),
            request.PathBase.ToUriComponent(),
            "/",
            relativePath);
    }
}
