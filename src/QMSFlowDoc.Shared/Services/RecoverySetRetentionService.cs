using System.Text.Json;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.Services;

/// <summary>
/// Applies retention only to published, self-contained recovery sets. Staging
/// directories and malformed folders are left untouched for operator review.
/// </summary>
public static class RecoverySetRetentionService
{
    public static async Task<int> ApplyAsync(
        string recoveryRootPath,
        int retentionDays,
        int minimumCopies,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(recoveryRootPath))
        {
            return 0;
        }

        var safeMinimumCopies = Math.Max(1, minimumCopies);
        var cutoffUtc = DateTime.UtcNow.Date.AddDays(-Math.Max(0, retentionDays));
        var recoverySets = new List<(string Path, DateTime CreatedAtUtc)>();

        foreach (var directory in Directory.EnumerateDirectories(recoveryRootPath))
        {
            ct.ThrowIfCancellationRequested();
            if (Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal))
            {
                continue;
            }

            var manifestPath = Path.Combine(directory, RecoverySetService.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<RecoverySetManifest>(
                    await File.ReadAllTextAsync(manifestPath, ct));
                if (manifest is not null && string.Equals(manifest.Status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    recoverySets.Add((directory, manifest.CreatedAtUtc));
                }
            }
            catch (JsonException)
            {
                // Leave unknown data for an operator; never delete it automatically.
            }
        }

        var ordered = recoverySets.OrderByDescending(set => set.CreatedAtUtc).ToList();
        var candidates = ordered
            .Skip(safeMinimumCopies)
            .Where(set => set.CreatedAtUtc < cutoffUtc)
            .ToList();

        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            Directory.Delete(candidate.Path, recursive: true);
        }

        return candidates.Count;
    }
}
