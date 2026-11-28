using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGest.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace BaGest.Database.MySql;

public class MySqlContext : AbstractContext<MySqlContext>
{
    private readonly DatabaseOptions _bagestOptions;
    private readonly MySqlServerVersionProvider _serverVersionProvider;

    /// <summary>
    /// The MySQL Server error code for when a unique constraint is violated.
    /// </summary>
    private const int UniqueConstraintViolationErrorCode = 1062;

    public MySqlContext(
        DbContextOptions<MySqlContext> efOptions,
        IOptionsSnapshot<BaGestOptions> bagestOptions,
        MySqlServerVersionProvider serverVersionProvider) : base(efOptions)
    {
        _bagestOptions = bagestOptions.Value.Database!;
        _serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
    }

    public override bool IsUniqueConstraintViolationException(DbUpdateException exception)
    {
        return exception.InnerException is MySqlException mysqlException &&
               mysqlException.Number == UniqueConstraintViolationErrorCode;
    }

    /// <summary>
    /// MySQL does not support LIMIT clauses in subqueries for certain subquery operators.
    /// See: https://dev.mysql.com/doc/refman/8.0/en/subquery-restrictions.html
    /// </summary>
    public override bool SupportsLimitInSubqueries => false;

    /// <summary>
    /// The character set of all text columns. utf8mb4 stores any Unicode text, including package metadata such as
    /// authors with non-ASCII names.
    /// </summary>
    public const string CharSet = "utf8mb4";

    /// <summary>
    /// A case-insensitive collation available on both MySQL and MariaDB. Package IDs and versions are compared
    /// case-insensitively, as they were with the previous latin1_swedish_ci collation.
    /// </summary>
    public const string Collation = "utf8mb4_unicode_ci";

    /// <summary>
    /// Columns limited to <see cref="AbstractContext{TContext}.DefaultMaxStringLength"/> characters are stored as
    /// longtext: as utf8mb4 varchar(4000) columns, they would exceed MySQL's 65,535-byte row size limit.
    /// </summary>
    public const string LongStringColumnType = "longtext";

    /// <summary>
    /// The command timeout while migrating. Converting a large Packages table copies it, which takes much longer
    /// than the default timeout of 30 seconds allows.
    /// </summary>
    public static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromMinutes(30);

    public override async Task RunMigrationsAsync(CancellationToken cancellationToken)
    {
        var commandTimeout = Database.GetCommandTimeout();
        Database.SetCommandTimeout(MigrationCommandTimeout);
        try
        {
            await base.RunMigrationsAsync(cancellationToken);
        }
        finally
        {
            Database.SetCommandTimeout(commandTimeout);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCharSet(CharSet);
        modelBuilder.UseCollation(Collation);

        base.OnModelCreating(modelBuilder);

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetProperties()))
        {
            if (property.GetMaxLength() == DefaultMaxStringLength)
            {
                property.SetColumnType(LongStringColumnType);
            }
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseMySql(_bagestOptions.ConnectionString, _serverVersionProvider.GetServerVersion(_bagestOptions));
        }
    }
}
