using System;
using System.Threading.Tasks;
using BaGest.Tests.Shared.Support;
using BaGest.Tests.Shared.Search;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Abstractions;

namespace BaGest.Database.PostgreSql.Tests;

/// <summary>
/// Runs against a real PostgreSQL server, configured the same way as <see cref="PostgreSqlVersionColumnTests"/>.
/// </summary>
public class PostgreSqlSearchTests(ITestOutputHelper output) : DatabaseSearchServiceFacts<PostgreSqlContext>(output)
{
    private const string ConnectionStringVariable = "BAGEST_TEST_POSTGRES";

    private readonly string _serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
    private readonly string _databaseName = "bagest_" + Guid.NewGuid().ToString("N");

    private string DatabaseConnectionString =>
        new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = _databaseName, Pooling = false }.ConnectionString;

    protected override bool IsAvailable => _serverConnectionString != null;

    protected override Task CreateDatabaseAsync() => ExecuteOnServerAsync($"CREATE DATABASE \"{_databaseName}\"");

    protected override Task DropDatabaseAsync() => ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");

    protected override PostgreSqlContext CreateContext(DbContextOptions<PostgreSqlContext> options) =>
        new(options, StaticOptionsSnapshot.ForConnectionString(DatabaseConnectionString));

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
