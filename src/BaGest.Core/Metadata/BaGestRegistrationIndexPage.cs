using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using BaGest.Protocol.Models;

namespace BaGest.Core;

/// <summary>
/// BaGest's extensions to a registration index page.
/// </summary>
/// <remarks>Extends <see cref="RegistrationIndexPage"/>.</remarks>
public class BaGestRegistrationIndexPage : RegistrationIndexPage
{
    /// <summary>
    /// <see langword="null"/> if this package's registration is paged. The items can be found
    /// by following the page's <see cref="RegistrationIndexPage.RegistrationPageUrl"/>.
    /// </summary>
    /// <remarks>This was modified to use BaGest's extended registration index page item model.</remarks>
    [JsonPropertyName("items")]
    [JsonPropertyOrder(int.MaxValue)]
    public new IReadOnlyList<BaGetRegistrationIndexPageItem> ItemsOrNull { get; set; } = Array.Empty<BaGetRegistrationIndexPageItem>();
}
