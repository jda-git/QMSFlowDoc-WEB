using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace QMSFlowDoc.BackupService.Services;

/// <summary>
/// Performs SQL Server database backups using BACKUP DATABASE T-SQL command.
/// Generates timestamped .bak files in the configured backup directory.
/// Automatically verifies backup integrity with RESTORE VERIFYONLY.
/// </summary>
public class SqlBackupService
{
    private readonly ILogger<SqlBackupService> _logger;

    public SqlBackupService(ILogger<SqlBackupService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes a full database backup and verifies it with RESTORE VERIFYONLY.
    /// Returns (path, verified) tuple. Path is null on backup failure.
    /// </summary>
    public async Task<(string? Path, bool Verified)> BackupDatabaseAsync(
        string connectionString, string databaseName, string backupDirectory, CancellationToken ct = default)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
        var backupFileName = $"{databaseName}_{timestamp}.bak";
        var dbBackupDir = Path.Combine(backupDirectory, "DB");
        Directory.CreateDirectory(dbBackupDir);
        var backupPath = Path.Combine(dbBackupDir, backupFileName);

        _logger.LogInformation("Starting SQL backup: {Database} → {Path}", databaseName, backupPath);

        try
        {
            var sql = $"BACKUP DATABASE [{databaseName}] TO DISK = @path WITH FORMAT, INIT, NAME = @name, COMPRESSION";

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            using var cmd = new SqlCommand(sql, connection);
            cmd.CommandTimeout = 600; // 10 minutes max
            cmd.Parameters.AddWithValue("@path", backupPath);
            cmd.Parameters.AddWithValue("@name", $"QMSFlowDoc Backup {timestamp}");

            await cmd.ExecuteNonQueryAsync(ct);

            // Verify file was created
            var fi = new FileInfo(backupPath);
            if (fi.Exists)
            {
                _logger.LogInformation("SQL backup completed: {Path} ({Size:N0} bytes)", backupPath, fi.Length);

                // Verify backup integrity with RESTORE VERIFYONLY
                var verified = await VerifyBackupIntegrityAsync(connection, backupPath, ct);
                return (backupPath, verified);
            }
            else
            {
                _logger.LogError("SQL backup command succeeded but file not found: {Path}", backupPath);
                return (null, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL backup failed for database {Database}", databaseName);
            return (null, false);
        }
    }

    /// <summary>
    /// Verifies a backup file using RESTORE VERIFYONLY on an existing connection.
    /// </summary>
    private async Task<bool> VerifyBackupIntegrityAsync(SqlConnection connection, string backupPath, CancellationToken ct)
    {
        _logger.LogInformation("Verifying backup integrity with RESTORE VERIFYONLY: {Path}", backupPath);
        try
        {
            var sql = "RESTORE VERIFYONLY FROM DISK = @path";
            using var cmd = new SqlCommand(sql, connection);
            cmd.CommandTimeout = 600;
            cmd.Parameters.AddWithValue("@path", backupPath);
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogInformation("✅ Backup integrity verification PASSED: {Path}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "❌ Backup integrity verification FAILED: {Path}. The backup file may be corrupt!", backupPath);
            return false;
        }
    }
}
