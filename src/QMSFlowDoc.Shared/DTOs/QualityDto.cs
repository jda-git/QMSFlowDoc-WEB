using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class NCListDto
{
    public Guid Id { get; set; }
    public DateTime DetectedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public NCSeverity Severity { get; set; }
    public NCStatus Status { get; set; }
    public bool ImpactPatient { get; set; }
    public int ActionCount { get; set; }
    public DateTime? DueDate { get; set; }

    public string? Origin { get; set; } // ISO 15189
    public string? RootCauseAnalysis { get; set; } // ISO 15189

    public NCListDto() { }
    public NCListDto(Guid id, DateTime det, string title, NCSeverity sev, NCStatus stat, bool impact, int actions, string? origin, string? rca, DateTime? dueDate = null)
    {
        Id = id; DetectedAt = det; Title = title; Severity = sev; Status = stat; ImpactPatient = impact; ActionCount = actions;
        Origin = origin; RootCauseAnalysis = rca; DueDate = dueDate;
    }
}

public record CreateNCRequest(
    string Title,
    string Description,
    NCSeverity Severity,
    NCStatus? Status,
    bool ImpactPatient,
    string? Containment,
    DateTime? DueDate,
    string? Origin,
    string? RootCauseAnalysis,
    Guid? DetectedByUserId = null // Added for local tracking
);

public record CreateCAPARequest(
    Guid NCId,
    CAPAActionType ActionType,
    string Description,
    Guid? OwnerUserId,
    DateTime? DueDate
);

// === Quejas / Complaints DTOs ===

public class ComplaintListDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplaintCategory Category { get; set; }
    public ComplaintStatus Status { get; set; }

    public ComplaintListDto() { }
}

public record CreateComplaintRequest(
    string Source,
    string Description,
    ComplaintCategory Category,
    string? InvestigationResult,
    string? CorrectiveAction,
    string? ResolutionEvidence = null
);
