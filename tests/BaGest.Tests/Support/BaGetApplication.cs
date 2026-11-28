using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BaGest.Authentication;
using BaGest.Core;
using BaGest.Web.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace BaGest.Tests;

public class BaGestApplication : WebApplicationFactory<Startup>
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _upstreamClient;
    private readonly Action<Dictionary<string, string>> _inMemoryConfiguration;

    public BaGestApplication(ITestOutputHelper output, HttpClient upstreamClient = null, Action<Dictionary<string, string>> inMemoryConfiguration = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _upstreamClient = upstreamClient;
        this._inMemoryConfiguration = inMemoryConfiguration;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Each host would otherwise watch its configuration files for changes. The tests create a host per
        // test, and the watchers they leave open exhaust the inotify instances a Linux user may have (128 by default).
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string>
        {
            { "hostBuilder:reloadConfigOnChange", "false" },
        }));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Create temporary storage paths.
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "BaGestTests",
            Guid.NewGuid().ToString("N"));
        var sqlitePath = Path.Combine(tempPath, "BaGest.db");
        var storagePath = Path.Combine(tempPath, "Packages");

        Directory.CreateDirectory(tempPath);

        builder
            .UseStartup<Startup>()
            .UseEnvironment("Production")
            .ConfigureLogging(logging =>
            {
                // BaGest uses console logging by default. This logger throws operation
                // cancelled exceptions when the host shuts down, causing the the debugger
                // to pause repeatedly if CLR exceptions are enabled.
                logging.ClearProviders();

                // Pipe logs to the xunit output.
                logging.AddProvider(new XunitLoggerProvider(_output));
            })
            .ConfigureAppConfiguration(config =>
            {
                // Setup the integration test configuration.
                var dict = new Dictionary<string, string>
                {
                    { "Database:Type", "Sqlite" },
                    { "Database:ConnectionString", $"Data Source={sqlitePath}" },
                    { "Storage:Type", "FileSystem" },
                    { "Storage:Path", storagePath },
                    { "Search:Type", "Database" },
                    { "Mirror:Enabled", _upstreamClient != null ? "true": "false" },
                    { "Mirror:PackageSource", "http://localhost/v3/index.json" },
                };
                _inMemoryConfiguration?.Invoke(dict);

                config.AddInMemoryCollection(dict);

            })
            .ConfigureServices((context, services) =>
            {
                // Make time deterministic for testing purposes.
                var time = new Mock<SystemTime>();
                time
                    .Setup(t => t.UtcNow)
                    .Returns(DateTime.Parse("2020-01-01T00:00:00.000Z"));

                services.AddSingleton(time.Object);
                if (_upstreamClient != null)
                {
                    services.AddSingleton(_upstreamClient);
                }

                services.Configure<HealthCheckServiceOptions>(opts => opts.Registrations.Clear());

                // Setup the integration test database.
                var provider = services.BuildServiceProvider();
                var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

                // Ensure the database is created before we run migrations. The migrations
                // can create the database too, however, migrations check whether the database exists
                // first. The SQLite provider implements this by attempting to open a connection,
                // and if that fails, creating the database. This throws several exceptions that
                // pauses the debugger repeatedly if CLR exceptions are enabled.
                // See: https://github.com/dotnet/efcore/blob/644d3c8c3a604fd0121d90eaf34f14870e19bcff/src/EFCore.Sqlite.Core/Storage/Internal/SqliteDatabaseCreator.cs#L88-L98
                using var scope = scopeFactory.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<IContext>();
                var dbCreator = ctx.Database.GetService<IRelationalDatabaseCreator>();

                dbCreator.Create();
                ctx.Database.Migrate();
            });
    }
}

internal static class BaGetWebApplicationFactoryExtensions
{
    public static async Task AddPackageAsync(
        this WebApplicationFactory<Startup> factory,
        Stream package,
        CancellationToken cancellationToken = default)
    {
        var scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();

        using var scope = scopeFactory.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<IPackageIndexingService>();

        var result = await indexer.IndexAsync(package, cancellationToken: cancellationToken);
        if (result != PackageIndexingResult.Success)
        {
            throw new InvalidOperationException($"Unexpected indexing result {result}");
        }
    }

    public static async Task AddSymbolPackageAsync(
        this WebApplicationFactory<Startup> factory,
        Stream symbolPackage,
        CancellationToken cancellationToken = default)
    {
        var scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();

        using var scope = scopeFactory.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<ISymbolIndexingService>();

        var result = await indexer.IndexAsync(symbolPackage, cancellationToken);
        if (result != SymbolIndexingResult.Success)
        {
            throw new InvalidOperationException($"Unexpected indexing result {result}");
        }
    }
}
