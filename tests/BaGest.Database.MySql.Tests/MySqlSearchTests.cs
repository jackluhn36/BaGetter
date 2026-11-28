using System.Threading.Tasks;
using BaGest.Database.MySql.Tests.Support;
using BaGest.Tests.Shared.Search;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace BaGest.Database.MySql.Tests;

public class MySqlSearchTests(ITestOutputHelper output) : DatabaseSearchServiceFacts<MySqlContext>(output)
{
    private readonly MySqlTestDatabase _database = new();
    private readonly MySqlServerVersionProvider _serverVersionProvider = new();

    protected override bool IsAvailable => _database.IsAvailable;

    protected override Task CreateDatabaseAsync() => _database.InitializeAsync();

    protected override Task DropDatabaseAsync() => _database.DisposeAsync();

    protected override MySqlContext CreateContext(DbContextOptions<MySqlContext> options) =>
        new(options, StaticOptionsSnapshot.ForConnectionString(_database.ConnectionString), _serverVersionProvider);
}
