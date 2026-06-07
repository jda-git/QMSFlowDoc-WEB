using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QMSFlowDoc.Shared.Services;

namespace QMSFlowDoc.BackupService.Services;

/// <summary>
/// Represents a single backup entry in the manifest.
/// </summary>
public class BackupManifestEntry
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = string.Empty; // "DB" or "Files"
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public int? FileCount { get; set; }
    public string? FileManifestPath { get; set; }
    public bool VerifyOnlyPassed { get; set; }
    public string Status { get; set; } = "OK"; // "OK", "FAILED", "VERIFY_FAILED"
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Tracks backup operations in a persistent JSON manifest for audit and ISO compliance.
/// </summary>
public class BackupManifestService
{
    private readonly ILogger<BackupManifestService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public BackupManifestService(ILogger<BackupManifestService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds an entry to the backup manifest.
    /// </summary>
    public async Task AddEntryAsync(string backupBasePath, BackupManifestEntry entry)
    {
        var manifestPath = GetManifestPath(backupBasePath);
        var entries = await LoadEntriesAsync(manifestPath);
        entries.Add(entry);
        await SaveEntriesAsync(manifestPath, entries);
        _logger.LogInformation("Manifest updated: {Type} {Status} → {Path}", entry.Type, entry.Status, entry.Path);
    }

    /// <summary>
    /// Reads the current manifest entries.
    /// </summary>
    public async Task<List<BackupManifestEntry>> GetEntriesAsync(string backupBasePath)
    {
        return await LoadEntriesAsync(GetManifestPath(backupBasePath));
    }

    /// <summary>
    /// Removes entries from the manifest whose paths no longer exist.
    /// </summary>
    public async Task CleanupEntriesAsync(string backupBasePath)
    {
        var manifestPath = GetManifestPath(backupBasePath);
        var entries = await LoadEntriesAsync(manifestPath);
        var before = entries.Count;

        entries.RemoveAll(e =>
        {
            if (e.Type == "DB") return !File.Exists(e.Path);
            return !Directory.Exists(e.Path);
        });

        if (entries.Count < before)
        {
            await SaveEntriesAsync(manifestPath, entries);
            _logger.LogInformation("Manifest cleanup: removed {Count} stale entries", before - entries.Count);
        }
    }

    /// <summary>
    /// Computes SHA256 hash of a file.
    /// </summary>
    public static async Task<string> ComputeSha256Async(string filePath)
    {
        return await FileBackupManifestService.ComputeSha256Async(filePath);
    }

    /// <summary>
    /// Writes the last backup status to a simple JSON file for the configurator to read.
    /// </summary>
    public async Task WriteLastStatusAsync(string backupBasePath, BackupManifestEntry dbEntry, BackupManifestEntry? fileEntry)
    {
        var statusPath = System.IO.Path.Combine(backupBasePath, "last_backup_status.json");
        var status = new
        {
            LastRun = DateTime.Now,
            Database = new { dbEntry.Status, dbEntry.Path, dbEntry.SizeBytes, dbEntry.VerifyOnlyPassed },
            Files = fileEntry != null
                ? new { fileEntry.Status, fileEntry.Path, fileEntry.SizeBytes, fileEntry.FileCount, fileEntry.FileManifestPath, VerifyOnlyPassed = fileEntry.VerifyOnlyPassed }
                : null
        };
        var json = JsonSerializer.Serialize(status, JsonOpts);
        await File.WriteAllTextAsync(statusPath, json);
    }

    private static string GetManifestPath(string backupBasePath) =>
        System.IO.Path.Combine(backupBasePath, "manifest.json");

    private static async Task<List<BackupManifestEntry>> LoadEntriesAsync(string path)
    {
        if (!File.Exists(path)) return new List<BackupManifestEntry>();
        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<BackupManifestEntry>>(json, JsonOpts) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static async Task SaveEntriesAsync(string path, List<BackupManifestEntry> entries)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(entries, JsonOpts);
        await File.WriteAllTextAsync(path, json);
    }
}
