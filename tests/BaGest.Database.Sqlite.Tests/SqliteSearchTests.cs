using System;
using System.IO;
using System.Threading.Tasks;
using BaGest.Tests.Shared.Support;
using BaGest.Tests.Shared.Search;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace BaGest.Database.Sqlite.Tests;

public class SqliteSearchTests(ITestOutputHelper output) : DatabaseSearchServiceFacts<SqliteContext>(output)
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), "bagest_" + Guid.NewGuid().ToString("N") + ".db");

    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = _databasePath,
        Pooling = false,
    }.ConnectionString;

    protected override bool IsAvailable => true;

    protected override Task CreateDatabaseAsync() => Task.CompletedTask;

    protected override Task DropDatabaseAsync()
    {
        File.Delete(_databasePath);
        return Task.CompletedTask;
    }

    protected override SqliteContext CreateContext(DbContextOptions<SqliteContext> options) =>
        new(options, StaticOptionsSnapshot.ForConnectionString(ConnectionString));
}
