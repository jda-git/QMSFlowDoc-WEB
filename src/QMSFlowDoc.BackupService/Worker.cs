using QMSFlowDoc.BackupService.Services;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.BackupService;

/// <summary>
/// Background worker that runs as a Windows Service.
/// Reads serversettings.json, computes next backup times, and executes
/// automatic backups (SQL + Documents) 1 or 2 times per day.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly SqlBackupService _sqlBackup;
    private readonly FileBackupService _fileBackup;
    private readonly RetentionService _retention;
    private readonly BackupManifestService _manifest;

    public Worker(
        ILogger<Worker> logger,
        SqlBackupService sqlBackup,
        FileBackupService fileBackup,
        RetentionService retention,
        BackupManifestService manifest)
    {
        _logger = logger;
        _sqlBackup = sqlBackup;
        _fileBackup = fileBackup;
        _retention = retention;
        _manifest = manifest;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QMSFlowDoc Backup Service started at {Time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = ServerSettingsService.Load();

                if (!settings.BackupEnabled)
                {
                    _logger.LogDebug("Backup is disabled. Checking again in 60 seconds.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                var nextBackup = CalculateNextBackupTime(settings);
                var waitTime = nextBackup - DateTime.Now;

                if (waitTime > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next backup scheduled at {Time}. Waiting {Wait:hh\\:mm\\:ss}...",
                        nextBackup.ToString("yyyy-MM-dd HH:mm"), waitTime);

                    // Wait until the scheduled time, checking every minute for config changes
                    while (DateTime.Now < nextBackup && !stoppingToken.IsCancellationRequested)
                    {
                        var remaining = nextBackup - DateTime.Now;
                        var sleepTime = remaining < TimeSpan.FromMinutes(1) ? remaining : TimeSpan.FromMinutes(1);
                        if (sleepTime > TimeSpan.Zero)
                            await Task.Delay(sleepTime, stoppingToken);
                    }
                }

                if (stoppingToken.IsCancellationRequested) break;

                // Reload settings in case they changed while waiting
                settings = ServerSettingsService.Load();

                await RunBackupAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in backup loop. Retrying in 5 minutes.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("QMSFlowDoc Backup Service stopped at {Time}", DateTimeOffset.Now);
    }

    /// <summary>
    /// Runs the full backup process: SQL → Documents → Retention cleanup.
    /// </summary>
    public async Task RunBackupAsync(ServerSettings settings, CancellationToken ct)
    {
        _logger.LogInformation("═══ Backup process started ═══");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. SQL Server backup
        var connStr = settings.BuildConnectionString();
        var (dbPath, dbVerified) = await _sqlBackup.BackupDatabaseAsync(
            connStr, settings.DatabaseName, settings.BackupPath, ct);

        // Record DB backup in manifest
        var dbEntry = new BackupManifestEntry
        {
            Timestamp = DateTime.Now,
            Type = "DB",
            Path = dbPath ?? string.Empty,
            Status = dbPath != null ? (dbVerified ? "OK" : "VERIFY_FAILED") : "FAILED",
            VerifyOnlyPassed = dbVerified
        };

        if (dbPath != null)
        {
            try
            {
                var fi = new System.IO.FileInfo(dbPath);
                dbEntry.SizeBytes = fi.Length;
                dbEntry.Sha256 = await BackupManifestService.ComputeSha256Async(dbPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not compute hash for backup: {Path}", dbPath);
            }
        }

        await _manifest.AddEntryAsync(settings.BackupPath, dbEntry);

        // 2. Document files backup
        QMSFlowDoc.Shared.Models.FileBackupResult? fileResult = null;
        BackupManifestEntry? fileEntry = null;
        if (!string.IsNullOrWhiteSpace(settings.DocumentRepositoryPath))
        {
            fileResult = await _fileBackup.BackupFilesAsync(
                settings.DocumentRepositoryPath, settings.BackupPath, ct);

            fileEntry = new BackupManifestEntry
            {
                Timestamp = DateTime.Now,
                Type = "Files",
                Path = fileResult?.Path ?? string.Empty,
                Status = fileResult != null ? (fileResult.Verified ? "OK" : "VERIFY_FAILED") : "FAILED",
                VerifyOnlyPassed = fileResult?.Verified == true,
                FileCount = fileResult?.FileCount,
                FileManifestPath = fileResult?.ManifestPath,
                Sha256 = fileResult?.ManifestSha256
            };

            if (fileResult != null && Directory.Exists(fileResult.Path))
            {
                fileEntry.SizeBytes = fileResult.SizeBytes;
            }

            await _manifest.AddEntryAsync(settings.BackupPath, fileEntry);
        }
        else
        {
            _logger.LogWarning("Document repository path not configured. Skipping file backup.");
        }

        // 3. Write last status for configurator
        await _manifest.WriteLastStatusAsync(settings.BackupPath, dbEntry, fileEntry);

        // 4. Retention cleanup
        await _retention.CleanupAsync(settings.BackupPath, settings.BackupRetentionDays, settings.BackupMinimumCopies, ct);

        // 5. Cleanup stale manifest entries
        await _manifest.CleanupEntriesAsync(settings.BackupPath);

        sw.Stop();
        _logger.LogInformation(
            "═══ Backup process completed in {Elapsed} ═══ DB: {DbStatus} (Verified: {DbVerified}), Files: {FileStatus}",
            sw.Elapsed.ToString(@"hh\:mm\:ss"),
            dbPath != null ? "OK" : "FAILED",
            dbVerified,
            fileResult != null ? fileEntry?.Status : "FAILED/SKIPPED");
    }

    /// <summary>
    /// Calculates the next backup time based on the configured schedules.
    /// </summary>
    private static DateTime CalculateNextBackupTime(ServerSettings settings)
    {
        var now = DateTime.Now;
        var candidates = new List<DateTime>();

        if (TryParseTime(settings.BackupTime1, out var time1))
        {
            var next1 = now.Date.Add(time1);
            if (next1 <= now) next1 = next1.AddDays(1);
            candidates.Add(next1);
        }

        if (settings.BackupTime2Enabled && TryParseTime(settings.BackupTime2, out var time2))
        {
            var next2 = now.Date.Add(time2);
            if (next2 <= now) next2 = next2.AddDays(1);
            candidates.Add(next2);
        }

        if (candidates.Count == 0)
        {
            // Fallback: 2:00 AM tomorrow
            return now.Date.AddDays(1).AddHours(2);
        }

        return candidates.Min();
    }

    private static bool TryParseTime(string timeStr, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(timeStr)) return false;

        var parts = timeStr.Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var hours) || !int.TryParse(parts[1], out var minutes)) return false;
        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) return false;

        result = new TimeSpan(hours, minutes, 0);
        return true;
    }
}
