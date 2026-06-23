using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class FolderDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public int SubFolderCount { get; set; }
    public int DocumentCount { get; set; }
    public int DisplayOrder { get; set; }

    public FolderDto() { }
    public FolderDto(Guid id, string name, Guid? parent, int subFolders, int docs)
    {
        Id = id; Name = name; ParentFolderId = parent; SubFolderCount = subFolders; DocumentCount = docs;
    }
    public FolderDto(Guid id, string name, Guid? parent, int subFolders, int docs, int displayOrder)
    {
        Id = id; Name = name; ParentFolderId = parent; SubFolderCount = subFolders; DocumentCount = docs; DisplayOrder = displayOrder;
    }
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public string DocCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? TypeName { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public Guid? FolderId { get; set; }
    public string? Area { get; set; }
    public string? Process { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DocumentStatus Status { get; set; }
    public int ReviewIntervalMonths { get; set; }
    public DateTime? NextReviewDue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CurrentVersionLabel { get; set; }

    public string NextReviewDisplay
    {
        get
        {
            if (!NextReviewDue.HasValue) return "N/A";
            var now = DateTime.UtcNow;
            if (NextReviewDue.Value < now) return "VENCIDO";
            
            var diff = NextReviewDue.Value - now;
            int totalMonths = (NextReviewDue.Value.Year - now.Year) * 12 + NextReviewDue.Value.Month - now.Month;
            
            if (totalMonths <= 0)
            {
                int days = (int)diff.TotalDays;
                return days <= 0 ? "VENCIDO" : $"{days} días";
            }
            return $"{totalMonths} meses";
        }
    }

    public DocumentDto() { }
    public DocumentDto(Guid id, string code, string title, string? type, Guid? folderId, string? area, string? process, DocumentStatus status, DateTime? next, DateTime created, string? ver)
    {
        Id = id; DocCode = code; Title = title; TypeName = type; FolderId = folderId; Area = area; Process = process;
        Status = status; NextReviewDue = next; CreatedAt = created; CurrentVersionLabel = ver;
    }
}

public record CreateDocumentRequest(
    string DocCode,
    string Title,
    Guid? DocumentTypeId,
    Guid? FolderId,
    string? Area,
    string? Process,
    int? ReviewIntervalMonths,
    string? VersionLabel, // Added for manual version input
    DocumentStatus? Status = null
);

public record UpdateDocumentStatusRequest(
    DocumentStatus NewStatus,
    string Comments
);

public record CreateVersionRequest(
    string VersionLabel,
    string ChangeSummary,
    string? FileName,
    string? MimeType
);

