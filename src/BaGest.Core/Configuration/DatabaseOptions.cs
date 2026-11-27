using System.ComponentModel.DataAnnotations;

namespace BaGest.Core;

public class DatabaseOptions
{ 
    public string Type { get; set; } = string.Empty;

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The SQLite journal mode (<c>DELETE</c>, <c>TRUNCATE</c>, <c>PERSIST</c>, <c>MEMORY</c>, <c>WAL</c> or <c>OFF</c>)
    /// to apply to the database on startup. Only used by the <c>Sqlite</c> database type.
    /// If not set, the journal mode of the database file is left unchanged.
    /// </summary>
    public string? JournalMode { get; set; }

    /// <summary>
    /// MySQL and MariaDB only: the server version, for example <c>8.4.0-mysql</c> or <c>11.4.0-mariadb</c>.
    /// When empty, the version is detected once from the server.
    /// </summary>
    public string ServerVersion { get; set; } = string.Empty;
}
