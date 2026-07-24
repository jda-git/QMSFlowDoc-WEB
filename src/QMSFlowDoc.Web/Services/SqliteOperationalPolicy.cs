using Microsoft.Data.Sqlite;

namespace QMSFlowDoc.Web.Services;

/// <summary>
/// Enforces the SQLite deployment assumptions for the single-server QMS setup.
/// The database must be local to the web host; network shares are rejected
/// because their locking semantics are not reliable enough for SQLite.
/// </summary>
public static class SqliteOperationalPolicy
{
    public static async Task VerifyAndConfigureAsync(string connectionString, CancellationToken ct = default)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("QMSFlowDoc requiere una base de datos SQLite basada en archivo.");
        }

        var databasePath = Path.GetFullPath(builder.DataSource);
        if (new Uri(databasePath).IsUnc)
        {
            throw new InvalidOperationException("La base de datos SQLite no puede estar en una ruta UNC o recurso compartido de red.");
        }

        var directory = Path.GetDirectoryName(databasePath)
            ?? throw new InvalidOperationException("La ruta de la base de datos no tiene directorio padre.");
        Directory.CreateDirectory(directory);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        await ExecutePragmaAsync(connection, "PRAGMA journal_mode=WAL;", ct);
        await ExecutePragmaAsync(connection, "PRAGMA synchronous=FULL;", ct);
        await ExecutePragmaAsync(connection, "PRAGMA foreign_keys=ON;", ct);
        await ExecutePragmaAsync(connection, "PRAGMA busy_timeout=10000;", ct);

        await using var integrityCommand = connection.CreateCommand();
        integrityCommand.CommandText = "PRAGMA quick_check;";
        var integrity = (await integrityCommand.ExecuteScalarAsync(ct))?.ToString();
        if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La base de datos SQLite no supera la comprobación de integridad. Use QMSFlowDoc.RecoveryTool.exe antes de iniciar la web.");
        }
    }

    private static async Task ExecutePragmaAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
