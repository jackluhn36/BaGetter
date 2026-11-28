using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BaGest.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BaGest.Tests;

public class ApiIntegrationTests : IDisposable
{
    private readonly BaGestApplication _app;
    private readonly HttpClient _client;

    private readonly Stream _packageStream;
    private readonly Stream _symbolPackageStream;

    private readonly ITestOutputHelper _output;

    public ApiIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _app = new BaGestApplication(_output);
        _client = _app.CreateClient();

        _packageStream = TestResources.GetResourceStream(TestResources.Package);
        _symbolPackageStream = TestResources.GetResourceStream(TestResources.SymbolPackage);
    }

    [Fact]
    public async Task IndexReturnsOk()
    {
        using var response = await _client.GetAsync("v3/index.json");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestData.ServiceIndex, content);
    }

    [Fact]
    public async Task SearchReturnsOk()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("v3/search");
        var content = await response.Content.ReadAsStreamAsync();
        var json = content.ToPrettifiedJson();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""@base"": ""http://localhost/v3/registration/""
  },
  ""totalHits"": 1,
  ""data"": [
    {
      ""id"": ""TestData"",
      ""version"": ""1.2.3"",
      ""description"": ""Test description"",
      ""authors"": [
        ""Test author""
      ],
      ""iconUrl"": """",
      ""licenseUrl"": """",
      ""projectUrl"": """",
      ""registration"": ""http://localhost/v3/registration/testdata/index.json"",
      ""summary"": """",
      ""tags"": [],
      ""title"": """",
      ""totalDownloads"": 0,
      ""versions"": [
        {
          ""@id"": ""http://localhost/v3/registration/testdata/1.2.3.json"",
          ""version"": ""1.2.3"",
          ""downloads"": 0
        }
      ]
    }
  ]
}", json);
    }

    [Fact]
    public async Task SearchReturnsEmpty()
    {
        using var response = await _client.GetAsync("v3/search?q=PackageDoesNotExist");
        var content = await response.Content.ReadAsStreamAsync();
        var json = content.ToPrettifiedJson();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""@base"": ""http://localhost/v3/registration/""
  },
  ""totalHits"": 0,
  ""data"": []
}", json);
    }

    [Fact]
    public async Task SearchMatchesDescription()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("v3/search?q=description");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await GetTotalHitsAsync(response));
    }

    [Theory]
    [InlineData("description%20TestData", 1)]
    [InlineData("description%20PackageDoesNotExist", 0)]
    public async Task SearchRequiresAllKeywords(string query, int totalHits)
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync($"v3/search?q={query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(totalHits, await GetTotalHitsAsync(response));
    }

    [Fact]
    public async Task SearchWithManyKeywordsReturnsOk()
    {
        var query = string.Join("%20", Enumerable.Repeat("test", 1000));

        using var response = await _client.GetAsync($"v3/search?q={query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("title-desc")]
    [InlineData("TITLE-DESC")]
    public async Task SearchSortsBySortByParameter(string sortBy)
    {
        await AddTwoPackagesAsync();

        using var response = await _client.GetAsync($"v3/search?sortBy={sortBy}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["TestData", "Another.Package"], await GetPackageIdsAsync(response));
    }

    [Theory]
    [InlineData("relevance")]
    [InlineData("unknown")]
    public async Task SearchWithDefaultSortByMatchesSearchWithout(string sortBy)
    {
        await AddTwoPackagesAsync();

        using var withoutSortBy = await _client.GetAsync("v3/search");
        using var withSortBy = await _client.GetAsync($"v3/search?sortBy={sortBy}");

        Assert.Equal(HttpStatusCode.OK, withSortBy.StatusCode);
        Assert.Equal(await withoutSortBy.Content.ReadAsStringAsync(), await withSortBy.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AutocompleteReturnsOk()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("v3/autocomplete");
        var content = await response.Content.ReadAsStreamAsync();
        var json = content.ToPrettifiedJson();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#""
  },
  ""totalHits"": 1,
  ""data"": [
    ""TestData""
  ]
}", json);
    }

    [Fact]
    public async Task AutocompleteReturnsEmpty()
    {
        using var response = await _client.GetAsync("v3/autocomplete?q=PackageDoesNotExist");
        var content = await response.Content.ReadAsStreamAsync();
        var json = content.ToPrettifiedJson();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#""
  },
  ""totalHits"": 0,
  ""data"": []
}", json);
    }

    [Fact]
    public async Task AutocompleteDoesNotMatchDescription()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("v3/autocomplete?q=description");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await GetTotalHitsAsync(response));
    }

    [Fact]
    public async Task AutocompleteVersionsReturnsOk()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("v3/autocomplete?id=TestData");
        var content = await response.Content.ReadAsStreamAsync();
        var json = content.ToPrettifiedJson();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#""
  },
  ""totalHits"": 1,
  ""data"": [
    ""1.2.3""
  ]
}", json);
    }

    [Fact]
    public async Task AutocompleteVersionsReturnsEmpty()
    {
        using var response = await _client.GetAsync("v3/autocomplete?id=PackageDoesNotExist");
        var content = await response.Content.ReadAsStreamAsync();
        var json = content.ToPrettifiedJson();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#""
  },
  ""totalHits"": 0,
  ""data"": []
}", json);
    }

    [Fact]
    public async Task VersionListReturnsOk()
    {
        await _app.AddPackageAsync(_packageStream);

        var response = await _client.GetAsync("v3/package/TestData/index.json");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(@"{""versions"":[""1.2.3""]}", content);
    }

    [Fact]
    public async Task VersionListReturnsNotFound()
    {
        using var response = await _client.GetAsync("v3/package/PackageDoesNotExist/index.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PackageDownloadReturnsOk()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("v3/package/TestData/1.2.3/TestData.1.2.3.nupkg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PackageDownloadReturnsNotFound()
    {
        using var response = await _client.GetAsync(
            "v3/package/PackageDoesNotExist/1.0.0/PackageDoesNotExist.1.0.0.nupkg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NuspecDownloadReturnsOk()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync(
            "v3/package/TestData/1.2.3/TestData.1.2.3.nuspec");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NuspecDownloadReturnsNotFound()
    {
        using var response = await _client.GetAsync(
            "v3/package/PackageDoesNotExist/1.0.0/PackageDoesNotExist.1.0.0.nuspec");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PackageMetadataReturnsOk()
    {
        // Arrange
        await _app.AddPackageAsync(_packageStream);

        const string expectedResponse = """
            {
              "@id": "http://localhost/v3/registration/testdata/index.json",
              "@type": [
                "catalog:CatalogRoot",
                "PackageRegistration",
                "catalog:Permalink"
              ],
              "count": 1,
              "items": [
                {
                  "@id": "http://localhost/v3/registration/testdata/index.json",
                  "count": 1,
                  "lower": "1.2.3",
                  "upper": "1.2.3",
                  "items": [
                    {
                      "@id": "http://localhost/v3/registration/testdata/1.2.3.json",
                      "packageContent": "http://localhost/v3/package/testdata/1.2.3/testdata.1.2.3.nupkg",
                      "catalogEntry": {
                        "downloads": 0,
                        "hasReadme": false,
                        "packageTypes": [
                          "Dependency"
                        ],
                        "releaseNotes": "",
                        "repositoryUrl": "",
                        "id": "TestData",
                        "version": "1.2.3",
                        "authors": "Test author",
                        "dependencyGroups": [
                          {
                            "targetFramework": "net5.0",
                            "dependencies": []
                          }
                        ],
                        "description": "Test description",
                        "iconUrl": "",
                        "language": "",
                        "licenseUrl": "",
                        "listed": true,
                        "minClientVersion": "",
                        "packageContent": "http://localhost/v3/package/testdata/1.2.3/testdata.1.2.3.nupkg",
                        "projectUrl": "",
                        "published": "2020-01-01T00:00:00Z",
                        "requireLicenseAcceptance": false,
                        "summary": "",
                        "tags": [],
                        "title": ""
                      }
                    }
                  ]
                }
              ],
              "totalDownloads": 0
            }
            """;

        // Act
        using var response = await _client.GetAsync("v3/registration/TestData/index.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStreamAsync();
        var actualResponse = content.ToPrettifiedJson();

        _output.WriteLine($"actual response:{Environment.NewLine}{actualResponse}");
        _output.WriteLine($"expected response:{Environment.NewLine}{expectedResponse}");

        Assert.Equal(expectedResponse, actualResponse);
    }

    [Fact]
    public async Task PackageMetadataReturnsNotFound()
    {
        using var response = await _client.GetAsync("v3/registration/PackageDoesNotExist/index.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PackageMetadataLeafReturnsOk()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("v3/registration/TestData/1.2.3.json");
        var content = await response.Content.ReadAsStreamAsync();
        var json = content.ToPrettifiedJson();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(@"{
  ""@id"": ""http://localhost/v3/registration/testdata/1.2.3.json"",
  ""@type"": [
    ""Package"",
    ""http://schema.nuget.org/catalog#Permalink""
  ],
  ""listed"": true,
  ""packageContent"": ""http://localhost/v3/package/testdata/1.2.3/testdata.1.2.3.nupkg"",
  ""published"": ""2020-01-01T00:00:00Z"",
  ""registration"": ""http://localhost/v3/registration/testdata/index.json""
}", json);
    }

    [Fact]
    public async Task PackageMetadataLeafReturnsNotFound()
    {
        using var response = await _client.GetAsync("v3/registration/PackageDoesNotExist/1.0.0.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PackageDependentsReturnsOk()
    {
        using var response = await _client.GetAsync("v3/dependents?packageId=TestData");

        var content = await response.Content.ReadAsStreamAsync();
        var json = content.ToPrettifiedJson();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(@"{
  ""totalHits"": 0,
  ""data"": []
}", json);
    }

    [Fact]
    public async Task PackageDependentsReturnsBadRequest()
    {
        using var response = await _client.GetAsync("v3/dependents");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SymbolDownloadReturnsOk()
    {
        await _app.AddPackageAsync(_packageStream);
        await _app.AddSymbolPackageAsync(_symbolPackageStream);

        using var response = await _client.GetAsync(
            "api/download/symbols/testdata.pdb/16F71ED8DD574AA2AD4A22D29E9C981Bffffffff/testdata.pdb");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("api/download/symbols/testdata.pdb/16F71ED8DD574AA2AD4A22D29E9C981B1/testdata.pdb")]
    [InlineData("api/download/symbols/testdata.pdb/16F71ED8DD574AA2AD4A22D29E9C981B/testdata.pdb")]
    [InlineData("api/download/symbols/testprefix/testdata.pdb/16F71ED8DD574AA2AD4A22D29E9C981Bffffffff/testdata.pdb")]
    public async Task MalformedSymbolDownloadReturnsOk(string uri)
    {
        await _app.AddPackageAsync(_packageStream);
        await _app.AddSymbolPackageAsync(_symbolPackageStream);

        using var response = await _client.GetAsync(uri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SymbolDownloadReturnsNotFound()
    {
        using var response = await _client.GetAsync(
            "api/download/symbols/doesnotexist.pdb/16F71ED8DD574AA2AD4A22D29E9C981Bffffffff/doesnotexist.pdb");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PushReturnsCreated()
    {
        using var content = new StreamContent(_packageStream);
        using var response = await _client.PutAsync("api/v2/package", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(
        "<authors>Test author</authors><description>Test description</description><dependencies><dependency id=\"Dependency\" version=\"[1.0\" /></dependencies>",
        "A dependency has an invalid version range")]
    [InlineData(
        "<authors>Test author</authors>",
        "The package must have a description")]
    public async Task PushOfInvalidManifestReturnsBadRequest(string metadata, string reason)
    {
        using var content = new StreamContent(CreatePackage(metadata));
        using var response = await _client.PutAsync("api/v2/package", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(reason, body);
        Assert.Equal(reason, response.ReasonPhrase);
    }

    [Fact]
    public async Task PushWhenStorageHoldsOrphanedFileReturnsConflictWithExplanation()
    {
        // Simulate a storage that is out of sync with the database: the package file is in the
        // storage, but the database has no entry for it.
        var storage = _app.Services.GetRequiredService<IStorageService>();
        var orphanPath = Path.Combine("packages", "testdata", "1.2.3", "testdata.1.2.3.nupkg");
        using var orphan = new MemoryStream(Encoding.UTF8.GetBytes("a different package"));
        Assert.Equal(StoragePutResult.Success, await storage.PutAsync(orphanPath, orphan, "binary/octet-stream"));

        using var content = new StreamContent(_packageStream);
        using var response = await _client.PutAsync("api/v2/package", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("out of sync", body);
        Assert.Contains("push again", body);
    }

    public void Dispose()
    {
        _app.Dispose();
        _client.Dispose();
    }

    private static async Task<int> GetTotalHitsAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("totalHits").GetInt32();
    }

    private async Task AddTwoPackagesAsync()
    {
        await _app.AddPackageAsync(_packageStream);
        await _app.AddPackageAsync(CreatePackage(
            "<authors>Test author</authors><description>Another description</description>",
            id: "Another.Package"));
    }

    private static async Task<string[]> GetPackageIdsAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        return json.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(result => result.GetProperty("id").GetString())
            .ToArray();
    }

    private static MemoryStream CreatePackage(string metadata, string id = "Invalid.Manifest")
    {
        var nuspec =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\">"
            + $"<metadata><id>{id}</id><version>1.0.0</version>" + metadata + "</metadata>"
            + "</package>";

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry($"{id}.nuspec").Open());
            writer.Write(nuspec);
        }

        stream.Position = 0;
        return stream;
    }
}
