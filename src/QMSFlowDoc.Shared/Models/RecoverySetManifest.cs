namespace QMSFlowDoc.Shared.Models;

/// <summary>
/// Describes one self-contained recovery point. A recovery point is usable only
/// when both the database and the complete document repository verify correctly.
/// </summary>
public sealed class RecoverySetManifest
{
    public const int CurrentFormatVersion = 2;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public string RecoverySetId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string DatabaseFileName { get; set; } = "database.db";
    public string DatabaseSha256 { get; set; } = string.Empty;
    public long DatabaseSizeBytes { get; set; }
    public bool DatabaseIntegrityPassed { get; set; }
    public string DocumentDirectoryName { get; set; } = "documents";
    public List<RecoverySetFileEntry> Documents { get; set; } = new();
    public string DocumentManifestSha256 { get; set; } = string.Empty;
    public string IntegrityAlgorithm { get; set; } = "SHA-256";
    public string? IntegrityHmac { get; set; }
    public string Status { get; set; } = "IN_PROGRESS";
}

public sealed class RecoverySetFileEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class RecoverySetVerificationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public RecoverySetManifest? Manifest { get; set; }
}

public sealed class RecoveryRestoreResult
{
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public string? SafetyDatabasePath { get; set; }
    public string? SafetyDocumentPath { get; set; }
    public string? ReportPath { get; set; }
}
