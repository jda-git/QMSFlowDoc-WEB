using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.Services;

/// <summary>
/// Builds and verifies per-file SHA-256 manifests for document repository backups.
/// </summary>
public static class FileBackupManifestService
{
    public const string ManifestFileName = "file-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<List<FileBackupManifestEntry>> BuildManifestAsync(
        string rootPath,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(rootPath))
            return new List<FileBackupManifestEntry>();

        var rootFullPath = Path.GetFullPath(rootPath);
        var entries = new List<FileBackupManifestEntry>();

        foreach (var file in Directory.EnumerateFiles(rootFullPath, "*", SearchOption.AllDirectories)
                     .Where(f => !Path.GetFileName(f).Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(f => Path.GetRelativePath(rootFullPath, f), StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            entries.Add(new FileBackupManifestEntry
            {
                RelativePath = NormalizeRelativePath(Path.GetRelativePath(rootFullPath, file)),
                SizeBytes = info.Length,
                Sha256 = await ComputeSha256Async(file, ct)
            });
        }

        return entries;
    }

    public static async Task<string> WriteManifestAsync(
        string rootPath,
        IReadOnlyCollection<FileBackupManifestEntry> entries,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(rootPath);
        var manifestPath = Path.Combine(rootPath, ManifestFileName);
        var json = JsonSerializer.Serialize(entries.OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase), JsonOptions);
        await File.WriteAllTextAsync(manifestPath, json, ct);
        return manifestPath;
    }

    public static async Task<bool> VerifyManifestAsync(
        string rootPath,
        IReadOnlyCollection<FileBackupManifestEntry> expectedEntries,
        CancellationToken ct = default)
    {
        var actualEntries = await BuildManifestAsync(rootPath, ct);
        if (actualEntries.Count != expectedEntries.Count)
            return false;

        var expected = expectedEntries
            .OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var actual = actualEntries
            .OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < expected.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (!string.Equals(expected[i].RelativePath, actual[i].RelativePath, StringComparison.OrdinalIgnoreCase))
                return false;
            if (expected[i].SizeBytes != actual[i].SizeBytes)
                return false;
            if (!string.Equals(expected[i].Sha256, actual[i].Sha256, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static string ComputeManifestSha256(IReadOnlyCollection<FileBackupManifestEntry> entries)
    {
        var canonical = JsonSerializer.Serialize(
            entries
                .OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(e => new { e.RelativePath, e.SizeBytes, Sha256 = e.Sha256.ToLowerInvariant() }));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');
}
