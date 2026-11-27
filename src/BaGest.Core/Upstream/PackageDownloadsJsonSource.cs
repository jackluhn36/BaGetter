using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace BaGest.Core;

/// <remarks>See: <see href="https://github.com/NuGet/NuGet.Services.Metadata/blob/master/src/NuGet.Indexing/Downloads.cs"/></remarks>
public class PackageDownloadsJsonSource : IPackageDownloadsSource
{
    public const string PackageDownloadsV1Url = "https://nugetprod0.blob.core.windows.net/ng-search-data/downloads.v1.json";

    private readonly HttpClient _httpClient;
    private readonly ILogger<PackageDownloadsJsonSource> _logger;

    public PackageDownloadsJsonSource(HttpClient httpClient, ILogger<PackageDownloadsJsonSource> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Dictionary<string, Dictionary<string, long>>> GetPackageDownloadsAsync()
    {
        _logger.LogInformation("Fetching package downloads...");

        var results = new Dictionary<string, Dictionary<string, long>>();

        using var downloadsStream = await GetDownloadsStreamAsync();
        using var downloadStreamReader = new StreamReader(downloadsStream);
        using var jsonReader = new JsonTextReader(downloadStreamReader);
        _logger.LogInformation("Parsing package downloads...");

        jsonReader.Read();

        while (jsonReader.Read())
        {
            try
            {
                if (jsonReader.TokenType == JsonToken.StartArray)
                {
                    // TODO: This line reads the entire document into memory...
                    var record = JToken.ReadFrom(jsonReader);

                    // The first entry should be the package id and the second entry should be an array of versions.
                    // This check protects against invalid entries.
                    if (record is not JArray array || array.Count != 2 || array[1] is not JArray)
                    {
                        continue;
                    }

                    var id = string.Intern(array[0].ToString().ToLowerInvariant());

                    if (!results.TryGetValue(id, out var value))
                    {
                        value = new Dictionary<string, long>();
                        results.Add(id, value);
                    }

                    foreach (var token in record)
                    {
                        if (token != null && token.Count() == 2)
                        {
                            var versionString = token[0]?.ToString();
                            if (versionString is null)
                            {
                                continue;
                            }

                            var version = string.Intern(NuGetVersion.Parse(versionString).ToNormalizedString().ToLowerInvariant());
                            var downloads = token[1]?.ToObject<long>() ?? 0;
                            value[version] = downloads;
                        }
                    }
                }
            }
            catch (JsonReaderException e)
            {
                _logger.LogError(e, "Invalid entry in downloads.v1.json");
            }
        }

        _logger.LogInformation("Parsed package downloads");

        return results;
    }

    private async Task<Stream> GetDownloadsStreamAsync()
    {
        _logger.LogInformation("Downloading downloads.v1.json...");

        var fileStream = File.Open(Path.GetTempFileName(), FileMode.Create);
        var response = await _httpClient.GetAsync(PackageDownloadsV1Url, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        using (var networkStream = await response.Content.ReadAsStreamAsync())
        {
            await networkStream.CopyToAsync(fileStream);
        }

        fileStream.Seek(0, SeekOrigin.Begin);

        _logger.LogInformation("Downloaded downloads.v1.json");

        return fileStream;
    }
}
