using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGest.Core;
using BaGest.Database.MySql.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace BaGest.Database.MySql.Tests;

public class MySqlCharSetTests : IAsyncLifetime
{
    private const string LastLatin1Migration = "20261124162116_SyncModelSnapshot";

    // Two-byte (Czech, Polish) and four-byte (emoji) UTF-8 characters, none of which latin1 can store.
    private const string NonLatin1Author = "Jan Havlíček";
    private const string NonLatin1Text = "Żółć 🚀";

    // Characters latin1 (cp1252 on MySQL) can store, so they may already be in databases created before utf8mb4.
    private const string Latin1Author = "François Müller";
    private const string Latin1Text = "Café © 2024, 10 €";

    private readonly MySqlTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [SkippableFact]
    public async Task FreshDatabase_StoresNonLatin1Metadata()
    {
        Skip.IfNot(_database.IsAvailable);

        await using (var context = _database.CreateContext())
        {
            await context.RunMigrationsAsync(CancellationToken.None);
            context.Packages.Add(CreatePackage("ClosedXML", "0.104.2", [NonLatin1Author, "Francois Botha"], NonLatin1Text));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var package = await context.Packages.SingleAsync();
            Assert.Equal([NonLatin1Author, "Francois Botha"], package.Authors);
            Assert.Equal(NonLatin1Text, package.Description);
        }
    }

    [SkippableFact]
    public async Task UpgradedDatabase_ConvertsExistingRowsAndStoresNonLatin1Metadata()
    {
        Skip.IfNot(_database.IsAvailable);

        await using (var context = _database.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(LastLatin1Migration);
        }

        // Inserted with SQL because the current model has columns the pre-upgrade schema does not have yet.
        await _database.ExecuteAsync($"""
            INSERT INTO Packages (Id, Version, OriginalVersion, Authors, Description, Tags,
                Published, Listed, Downloads, HasEmbeddedIcon, HasReadme, IsPrerelease, RequireLicenseAcceptance, SemVerLevel)
            VALUES ('Existing.Package', '1.0.0', '1.0.0', '["{Latin1Author}"]', '{Latin1Text}', '',
                UTC_TIMESTAMP(6), 1, 0, 0, 0, 0, 0, 0)
            """);

        await using (var context = _database.CreateContext())
        {
            await context.RunMigrationsAsync(CancellationToken.None);
            context.Packages.Add(CreatePackage("ClosedXML", "0.104.2", [NonLatin1Author], NonLatin1Text));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var existing = await context.Packages.SingleAsync(p => p.Id == "Existing.Package");
            Assert.Equal([Latin1Author], existing.Authors);
            Assert.Equal(Latin1Text, existing.Description);

            var added = await context.Packages.SingleAsync(p => p.Id == "ClosedXML");
            Assert.Equal([NonLatin1Author], added.Authors);
            Assert.Equal(NonLatin1Text, added.Description);
        }
    }

    [SkippableFact]
    public async Task UpgradedDatabaseWithCompactTables_IsConverted()
    {
        Skip.IfNot(_database.IsAvailable);

        await using (var context = _database.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(LastLatin1Migration);
        }

        // Tables created by servers older than MySQL 5.7.9 or MariaDB 10.2.2 use the COMPACT row format, whose
        // 767-byte index key limit the utf8mb4 indexes on these tables exceed. (Packages, with its nine latin1
        // varchar(4000) columns, does not fit in a COMPACT row in strict mode at all.)
        foreach (var table in new[] { "PackageDependencies", "PackageTypes", "TargetFrameworks" })
        {
            await _database.ExecuteAsync($"ALTER TABLE `{table}` ROW_FORMAT=COMPACT");
        }

        await using (var context = _database.CreateContext())
        {
            await context.RunMigrationsAsync(CancellationToken.None);
        }

        var compactTables = await _database.QueryScalarAsync<long>("""
            SELECT COUNT(*) FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME <> '__EFMigrationsHistory' AND ROW_FORMAT <> 'Dynamic'
            """);
        Assert.Equal(0, compactTables);
    }

    [SkippableFact]
    public async Task AllLongColumnsFilledWithMultiByteCharacters_FitInARow()
    {
        Skip.IfNot(_database.IsAvailable);

        var longText = new string('ż', AbstractContext<MySqlContext>.DefaultMaxStringLength);
        var longUrl = new Uri("https://example.com/" + new string('ł', AbstractContext<MySqlContext>.DefaultMaxStringLength - 100));

        await using (var context = _database.CreateContext())
        {
            await context.RunMigrationsAsync(CancellationToken.None);

            var package = CreatePackage("Long.Package", "1.0.0", [longText[..3990]], longText);
            package.Summary = longText;
            package.Tags = [longText[..3990]];
            package.IconUrl = longUrl;
            package.LicenseUrl = longUrl;
            package.ProjectUrl = longUrl;
            package.RepositoryUrl = longUrl;
            package.CachedFrom = longText;
            package.ReleaseNotes = longText;
            context.Packages.Add(package);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var package = await context.Packages.SingleAsync();
            Assert.Equal(longText, package.Description);
            Assert.Equal(longText, package.Summary);
            Assert.Equal(longText, package.CachedFrom);
            Assert.Equal(longUrl, package.RepositoryUrl);
        }
    }

    [SkippableFact]
    public async Task PackageIdAndVersion_StayCaseInsensitive()
    {
        Skip.IfNot(_database.IsAvailable);

        await using (var context = _database.CreateContext())
        {
            await context.RunMigrationsAsync(CancellationToken.None);
            context.Packages.Add(CreatePackage("Test.Package", "1.0.0-Beta", ["Author"], "Description"));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var found = await context.Packages
                .CountAsync(p => p.Id == "test.package" && p.NormalizedVersionString == "1.0.0-beta");
            Assert.Equal(1, found);

            context.Packages.Add(CreatePackage("TEST.PACKAGE", "1.0.0-BETA", ["Author"], "Description"));
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.True(context.IsUniqueConstraintViolationException(exception));
        }
    }

    [SkippableFact]
    public async Task AllTextColumns_UseUtf8mb4WithCaseInsensitiveCollation()
    {
        Skip.IfNot(_database.IsAvailable);

        await using (var context = _database.CreateContext())
        {
            await context.RunMigrationsAsync(CancellationToken.None);
        }

        var otherCollations = await _database.QueryScalarAsync<long>($"""
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME <> '__EFMigrationsHistory'
              AND CHARACTER_SET_NAME IS NOT NULL
              AND (CHARACTER_SET_NAME <> '{MySqlContext.CharSet}' OR COLLATION_NAME <> '{MySqlContext.Collation}')
            """);
        var databaseCollation = await _database.QueryScalarAsync<string>(
            "SELECT DEFAULT_COLLATION_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = DATABASE()");

        Assert.Equal(0, otherCollations);
        Assert.Equal(MySqlContext.Collation, databaseCollation);
    }

    [SkippableFact]
    public async Task RevertingTheMigration_RestoresLatin1ForAsciiData()
    {
        Skip.IfNot(_database.IsAvailable);

        await using (var context = _database.CreateContext())
        {
            await context.RunMigrationsAsync(CancellationToken.None);
            context.Packages.Add(CreatePackage("Ascii.Package", "1.0.0", ["Author"], "Plain ASCII description"));
            await context.SaveChangesAsync();

            await context.GetService<IMigrator>().MigrateAsync(LastLatin1Migration);
        }

        var authorsColumn = await _database.QueryScalarAsync<string>("""
            SELECT CONCAT(COLUMN_TYPE, ' ', CHARACTER_SET_NAME) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Packages' AND COLUMN_NAME = 'Authors'
            """);
        var description = await _database.QueryScalarAsync<string>("SELECT Description FROM Packages");

        Assert.Equal("varchar(4000) latin1", authorsColumn);
        Assert.Equal("Plain ASCII description", description);
    }

    private static Package CreatePackage(string id, string version, string[] authors, string description) => new()
    {
        Id = id,
        NormalizedVersionString = version,
        OriginalVersionString = version,
        Authors = authors,
        Description = description,
        Tags = [],
        Published = DateTime.UtcNow,
        Listed = true,
    };
}
