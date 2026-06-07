using Microsoft.Extensions.Logging;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.Services;

namespace QMSFlowDoc.BackupService.Services;

/// <summary>
/// Copies the central document repository to a timestamped backup folder.
/// V1: Full copy. Future: Incremental.
/// </summary>
public class FileBackupService
{
    private readonly ILogger<FileBackupService> _logger;

    public FileBackupService(ILogger<FileBackupService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Copies all files from sourcePath to a timestamped subfolder under backupBasePath/Files/.
    /// Returns verifiable manifest evidence for the backup, or null on failure.
    /// </summary>
    public async Task<FileBackupResult?> BackupFilesAsync(
        string sourcePath, string backupBasePath, CancellationToken ct = default)
    {
        if (!Directory.Exists(sourcePath))
        {
            _logger.LogError("Document source path does not exist: {Path}", sourcePath);
            return null;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
        var destDir = Path.Combine(backupBasePath, "Files", timestamp);

        _logger.LogInformation("Starting file backup: {Source} → {Dest}", sourcePath, destDir);

        try
        {
            Directory.CreateDirectory(destDir);
            var fileCount = 0;
            long totalBytes = 0;

            await Task.Run(() =>
            {
                CopyDirectoryRecursive(sourcePath, destDir, ref fileCount, ref totalBytes, ct);
            }, ct);

            _logger.LogInformation("File backup completed: {Count} files, {Bytes:N0} bytes → {Path}",
                fileCount, totalBytes, destDir);

            var manifestEntries = await FileBackupManifestService.BuildManifestAsync(destDir, ct);
            var manifestPath = await FileBackupManifestService.WriteManifestAsync(destDir, manifestEntries, ct);
            var manifestSha = FileBackupManifestService.ComputeManifestSha256(manifestEntries);
            var verified = await FileBackupManifestService.VerifyManifestAsync(destDir, manifestEntries, ct);

            if (!verified)
            {
                _logger.LogError("File backup verification failed after copy: {Path}", destDir);
            }

            return new FileBackupResult
            {
                Path = destDir,
                FileCount = manifestEntries.Count,
                SizeBytes = manifestEntries.Sum(e => e.SizeBytes),
                ManifestPath = manifestPath,
                ManifestSha256 = manifestSha,
                Verified = verified
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("File backup was cancelled.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File backup failed");
            return null;
        }
    }

    private static void CopyDirectoryRecursive(
        string sourceDir, string destDir, ref int fileCount, ref long totalBytes, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
            var fi = new FileInfo(destFile);
            fileCount++;
            totalBytes += fi.Length;
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var dirName = Path.GetFileName(subDir);
            // Skip temp and log directories
            if (dirName.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("_archived", StringComparison.OrdinalIgnoreCase))
                continue;

            CopyDirectoryRecursive(subDir, Path.Combine(destDir, dirName), ref fileCount, ref totalBytes, ct);
        }
    }
}
