namespace QMSFlowDoc.Domain.Entities;

public enum DocumentStatus
{
    DRAFT,
    REVIEW,
    APPROVED,
    OBSOLETE,
    RETIRED
}

public class Folder
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public List<Folder> SubFolders { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int DisplayOrder { get; set; } = 0;
}

public class Document
{
    public Guid Id { get; set; }
    public string DocCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid? DocumentTypeId { get; set; }
    public DocumentType? DocumentType { get; set; }
    public Guid? FolderId { get; set; }
    public Folder? Folder { get; set; }
    public string? Area { get; set; }
    public string? Process { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.DRAFT;
    public int? ReviewIntervalMonths { get; set; }
    public DateTime? NextReviewDue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<DocumentVersion> Versions { get; set; } = new();
}

public class DocumentVersion
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int VersionMajor { get; set; }
    public int VersionMinor { get; set; }
    public string VersionLabel { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveFrom { get; set; }
    public string? CloudFileId { get; set; }
    public string? CloudEtag { get; set; }
    public string? Sha256 { get; set; }
    public string? MimeType { get; set; }
    public string? FileName { get; set; }
    public string? LocalFilePath { get; set; }
    public bool IsCurrent { get; set; } = false;

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    
    // ISO 15189 Req 1.1 Approval Workflow
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovalDate { get; set; }
}

public class DocumentType
{
    public Guid Id { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
