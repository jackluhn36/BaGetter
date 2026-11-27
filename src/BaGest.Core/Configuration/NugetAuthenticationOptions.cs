using System;
using BaGest.Core.Configuration;

namespace BaGest.Core;

public sealed class NugetAuthenticationOptions
{
    /// <summary>
    /// Username and password credentials for downloading packages.
    /// </summary>
    public NugetCredentials[] Credentials { get; set; } = Array.Empty<NugetCredentials>();

    /// <summary>
    /// Api keys for pushing packages into the feed.
    /// </summary>
    public ApiKey[] ApiKeys { get; set; } = Array.Empty<ApiKey>();
}
