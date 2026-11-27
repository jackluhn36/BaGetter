using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGest.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BaGest.Database.Sqlite;

public class SqliteContext : AbstractContext<SqliteContext>
{
    private readonly DatabaseOptions _bagestOptions;

    /// <summary>
    /// The Sqlite error code for when a unique constraint is violated.
    /// </summary>
    private const int SqliteUniqueConstraintViolationErrorCode = 19;

    /// <summary>
    /// The journal modes supported by SQLite. The value is interpolated into a PRAGMA statement
    /// (which cannot be parameterized), so it must be one of these.
    /// </summary>
    private static readonly string[] SupportedJournalModes = ["DELETE", "TRUNCATE", "PERSIST", "MEMORY", "WAL", "OFF"];

    public SqliteContext(DbContextOptions<SqliteContext> efOptions, IOptionsSnapshot<BaGestOptions> bagestOptions)
        : base(efOptions)
    {
        _bagestOptions = bagestOptions.Value.Database;
    }

    public override bool IsUniqueConstraintViolationException(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException &&
            sqliteException.SqliteErrorCode == SqliteUniqueConstraintViolationErrorCode;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Package>()
            .Property(p => p.Id)
            .HasColumnType("TEXT COLLATE NOCASE");

        builder.Entity<Package>()
            .Property(p => p.NormalizedVersionString)
            .HasColumnType("TEXT COLLATE NOCASE");

        builder.Entity<PackageDependency>()
            .Property(d => d.Id)
            .HasColumnType("TEXT COLLATE NOCASE");

        builder.Entity<PackageType>()
            .Property(t => t.Name)
            .HasColumnType("TEXT COLLATE NOCASE");

        builder.Entity<TargetFramework>()
            .Property(f => f.Moniker)
            .HasColumnType("TEXT COLLATE NOCASE");
    }

    public override async Task RunMigrationsAsync(CancellationToken cancellationToken)
    {
        var journalMode = GetConfiguredJournalMode();

        if (Database.GetDbConnection() is SqliteConnection connection)
        {
            /* Create the folder of the Sqlite blob if it does not exist. */
            EnsureDataSourceDirectoryExists(connection);
        }

        await base.RunMigrationsAsync(cancellationToken);

        if (journalMode != null)
        {
            // The journal mode is a property of the database file, so it is applied after the
            // database has been created and migrated. The name was validated against a fixed list.
#pragma warning disable EF1002
            await Database.ExecuteSqlRawAsync($"PRAGMA journal_mode={journalMode};", cancellationToken);
#pragma warning restore EF1002
        }
    }

    /// <summary>
    /// Gets the validated, upper-cased value of the Database::JournalMode config, or null if it is not set.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configured journal mode is not supported by SQLite.</exception>
    private string? GetConfiguredJournalMode()
    {
        if (string.IsNullOrWhiteSpace(_bagestOptions.JournalMode)) return null;

        var journalMode = _bagestOptions.JournalMode.Trim().ToUpperInvariant();

        if (!SupportedJournalModes.Contains(journalMode))
        {
            throw new InvalidOperationException(
                $"Unsupported Database:JournalMode '{_bagestOptions.JournalMode}'. " +
                $"Supported values: {string.Join(", ", SupportedJournalModes)}.");
        }

        return journalMode;
    }

    /// <summary>
    /// Creates directories specified in the Database::ConnectionString config for the Sqlite database file.
    /// </summary>
    /// <param name="connection">Instance of the <see cref="SqliteConnection"/>.</param>
    private static void EnsureDataSourceDirectoryExists(SqliteConnection connection)
    {
        var pathToCreate = Path.GetDirectoryName(connection.DataSource);

        if (string.IsNullOrWhiteSpace(pathToCreate)) return;

        Directory.CreateDirectory(pathToCreate);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite(_bagestOptions.ConnectionString);
    }
}
