using Microsoft.Extensions.Logging;

namespace QMSFlowDoc.BackupService.Services;

/// <summary>
/// Cleans up old backups based on the retention policy (days),
/// while always preserving a minimum number of copies for safety.
/// </summary>
public class RetentionService
{
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(ILogger<RetentionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Deletes backup folders and files older than retentionDays,
    /// but always keeps at least minimumCopies of each backup type.
    /// </summary>
    public Task CleanupAsync(string backupBasePath, int retentionDays, int minimumCopies = 3, CancellationToken ct = default)
    {
        var cutoff = DateTime.Now.AddDays(-retentionDays);
        _logger.LogInformation(
            "Running retention cleanup: deleting backups older than {Cutoff:d} ({Days} days), minimum copies to keep: {Min}",
            cutoff, retentionDays, minimumCopies);

        int deletedDirs = 0, deletedFiles = 0;

        // Clean DB backups
        var dbDir = Path.Combine(backupBasePath, "DB");
        if (Directory.Exists(dbDir))
        {
            var allBaks = Directory.GetFiles(dbDir, "*.bak")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            // Identify candidates for deletion (older than cutoff)
            var toDelete = allBaks.Where(f => f.CreationTime < cutoff).ToList();

            // Ensure we keep at least minimumCopies
            var wouldRemain = allBaks.Count - toDelete.Count;
            if (wouldRemain < minimumCopies)
            {
                var mustKeep = minimumCopies - wouldRemain;
                if (mustKeep >= toDelete.Count)
                {
                    _logger.LogWarning(
                        "Retention: skipping all DB backup deletions to preserve minimum {Min} copies (have {Total})",
                        minimumCopies, allBaks.Count);
                    toDelete.Clear();
                }
                else
                {
                    // Remove the most recent ones from the delete list to keep minimumCopies
                    toDelete = toDelete
                        .OrderBy(f => f.CreationTime) // oldest first
                        .Take(toDelete.Count - mustKeep)
                        .ToList();
                    _logger.LogWarning(
                        "Retention: limiting DB backup deletions to keep minimum {Min} copies",
                        minimumCopies);
                }
            }

            foreach (var fi in toDelete)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    fi.Delete();
                    deletedFiles++;
                    _logger.LogDebug("Deleted old DB backup: {File}", fi.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup: {File}", fi.FullName);
                }
            }
        }

        // Clean File backups (timestamped folders)
        var filesDir = Path.Combine(backupBasePath, "Files");
        if (Directory.Exists(filesDir))
        {
            var allDirs = Directory.GetDirectories(filesDir)
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => d.CreationTime)
                .ToList();

            var toDeleteDirs = allDirs.Where(d => d.CreationTime < cutoff).ToList();

            // Ensure we keep at least minimumCopies
            var wouldRemain = allDirs.Count - toDeleteDirs.Count;
            if (wouldRemain < minimumCopies)
            {
                var mustKeep = minimumCopies - wouldRemain;
                if (mustKeep >= toDeleteDirs.Count)
                {
                    _logger.LogWarning(
                        "Retention: skipping all file backup deletions to preserve minimum {Min} copies (have {Total})",
                        minimumCopies, allDirs.Count);
                    toDeleteDirs.Clear();
                }
                else
                {
                    toDeleteDirs = toDeleteDirs
                        .OrderBy(d => d.CreationTime)
                        .Take(toDeleteDirs.Count - mustKeep)
                        .ToList();
                    _logger.LogWarning(
                        "Retention: limiting file backup deletions to keep minimum {Min} copies",
                        minimumCopies);
                }
            }

            foreach (var di in toDeleteDirs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    di.Delete(recursive: true);
                    deletedDirs++;
                    _logger.LogDebug("Deleted old file backup: {Dir}", di.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup dir: {Dir}", di.FullName);
                }
            }
        }

        _logger.LogInformation("Retention cleanup complete: {Files} DB files, {Dirs} file backups deleted", deletedFiles, deletedDirs);
        return Task.CompletedTask;
    }
}
