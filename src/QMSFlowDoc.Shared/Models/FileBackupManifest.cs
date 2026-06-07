namespace QMSFlowDoc.Shared.Models;

/// <summary>
/// Hash and size evidence for one backed-up document repository file.
/// </summary>
public class FileBackupManifestEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>
/// Result of a document repository backup with verifiable manifest evidence.
/// </summary>
public class FileBackupResult
{
    public string Path { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long SizeBytes { get; set; }
    public string ManifestPath { get; set; } = string.Empty;
    public string ManifestSha256 { get; set; } = string.Empty;
    public bool Verified { get; set; }
}
