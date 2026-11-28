using System;
using System.Threading.Tasks;
using BaGest.Tests.Shared.Support;
using BaGest.Tests.Shared.Search;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace BaGest.Database.SqlServer.Tests;

/// <summary>
/// Runs against a real SQL Server. Set <c>BAGEST_TEST_SQLSERVER</c> to a connection string whose login may create
/// databases (for example <c>Server=localhost,1433;User ID=sa;Password=secret;TrustServerCertificate=True</c>);
/// otherwise the tests are skipped.
/// </summary>
public class SqlServerSearchTests(ITestOutputHelper output) : DatabaseSearchServiceFacts<SqlServerContext>(output)
{
    private const string ConnectionStringVariable = "BAGEST_TEST_SQLSERVER";

    private readonly string _serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
    private readonly string _databaseName = "bagest_" + Guid.NewGuid().ToString("N");

    private string DatabaseConnectionString =>
        new SqlConnectionStringBuilder(_serverConnectionString) { InitialCatalog = _databaseName, Pooling = false }.ConnectionString;

    protected override bool IsAvailable => _serverConnectionString != null;

    protected override Task CreateDatabaseAsync() => ExecuteOnServerAsync($"CREATE DATABASE [{_databaseName}]");

    protected override Task DropDatabaseAsync() => ExecuteOnServerAsync(
        $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]");

    protected override SqlServerContext CreateContext(DbContextOptions<SqlServerContext> options) =>
        new(options, StaticOptionsSnapshot.ForConnectionString(DatabaseConnectionString));

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new SqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
