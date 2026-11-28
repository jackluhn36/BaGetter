using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BaGest.Tests;

public class AlpineLayoutIntegrationTests : IDisposable
{
    private readonly BaGestApplication _app;
    private readonly HttpClient _client;

    public AlpineLayoutIntegrationTests(ITestOutputHelper output)
    {
        _app = new BaGestApplication(output);
        _client = _app.CreateClient();
    }

    [Fact]
    public async Task LoadsAlpineDeferred()
    {
        var content = await GetPageAsync("/");

        var alpine = Regex.Match(content, @"<script\s(?<attributes>[^>]*src=""/_content/BaGest\.Web/lib/alpinejs/[^""]+""[^>]*)>");

        Assert.True(alpine.Success, "The layout does not load Alpine.");
        Assert.Contains("/lib/alpinejs/dist/cdn.min.js", alpine.Value);
        Assert.Matches(@"(^|\s)defer(\s|=|$)", alpine.Groups["attributes"].Value);
    }

    public void Dispose()
    {
        _client.Dispose();
        _app.Dispose();
    }

    private async Task<string> GetPageAsync(string path)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
