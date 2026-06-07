using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace QMSFlowDoc.BackupService.Services;

/// <summary>
/// Provides SQL Server backup verification (RESTORE VERIFYONLY) and database restoration.
/// </summary>
public class SqlRestoreService
{
    private readonly ILogger<SqlRestoreService> _logger;

    public SqlRestoreService(ILogger<SqlRestoreService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a .bak file using RESTORE VERIFYONLY.
    /// Returns true if the backup is valid.
    /// </summary>
    public async Task<bool> VerifyBackupAsync(string connectionString, string backupPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Verifying backup integrity: {Path}", backupPath);

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            var sql = "RESTORE VERIFYONLY FROM DISK = @path";
            using var cmd = new SqlCommand(sql, connection);
            cmd.CommandTimeout = 600;
            cmd.Parameters.AddWithValue("@path", backupPath);
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogInformation("Backup verification PASSED: {Path}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Backup verification FAILED: {Path}", backupPath);
            return false;
        }
    }

    /// <summary>
    /// Restores a database from a .bak file. USE WITH EXTREME CAUTION.
    /// </summary>
    public async Task<bool> RestoreDatabaseAsync(
        string connectionString, string databaseName, string backupPath, CancellationToken ct = default)
    {
        _logger.LogWarning("Starting database restore: {Database} ← {Path}", databaseName, backupPath);

        try
        {
            // Connect to master to perform restore
            var masterConn = connectionString.Replace($"Database={databaseName}", "Database=master");

            using var connection = new SqlConnection(masterConn);
            await connection.OpenAsync(ct);

            // Set database to single-user mode to disconnect other users
            var setSingleUser = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
            using (var cmd1 = new SqlCommand(setSingleUser, connection))
            {
                cmd1.CommandTimeout = 60;
                try { await cmd1.ExecuteNonQueryAsync(ct); }
                catch { /* Database may not exist yet */ }
            }

            // Perform restore
            var restoreSql = $"RESTORE DATABASE [{databaseName}] FROM DISK = @path WITH REPLACE, RECOVERY";
            using (var cmd2 = new SqlCommand(restoreSql, connection))
            {
                cmd2.CommandTimeout = 1800; // 30 minutes max
                cmd2.Parameters.AddWithValue("@path", backupPath);
                await cmd2.ExecuteNonQueryAsync(ct);
            }

            // Set back to multi-user mode
            var setMultiUser = $"ALTER DATABASE [{databaseName}] SET MULTI_USER";
            using (var cmd3 = new SqlCommand(setMultiUser, connection))
            {
                cmd3.CommandTimeout = 60;
                await cmd3.ExecuteNonQueryAsync(ct);
            }

            _logger.LogInformation("Database restore completed successfully: {Database}", databaseName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Database restore FAILED: {Database} ← {Path}", databaseName, backupPath);
            return false;
        }
    }
}
